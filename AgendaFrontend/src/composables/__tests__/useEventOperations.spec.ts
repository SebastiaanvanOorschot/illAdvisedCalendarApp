import { beforeEach, describe, expect, it, vi } from 'vitest'
import dayjs from 'dayjs'
import { CreateEventDto, EditOccurrenceDto, EventWithOwnerDto, UpdateEventDto } from '@/api/agenda-api-swagger'
import { useEventOperations, type EventFormData } from '../useEventOperations'

const api = vi.hoisted(() => ({
    eventsPOST: vi.fn(),
    eventsPUT: vi.fn(),
    eventsDELETE: vi.fn(),
    addException: vi.fn(),
    editOccurrence: vi.fn()
}))

// keep the generated DTO classes, replace only the client
vi.mock('@/api/agenda-api-swagger', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@/api/agenda-api-swagger')>()),
    AgendaAPI: vi.fn(function () {
        return api
    })
}))

const selectedDate = dayjs(new Date(2026, 2, 10)) // 10 March 2026, local time

const form = (overrides: Partial<EventFormData> = {}): EventFormData => ({
    title: 'Dentist',
    description: '',
    isAllDay: false,
    startTime: '09:30',
    endTime: '10:45',
    isRecurring: false,
    recurrencePattern: 'weekly',
    recurrenceInterval: 2,
    recurrenceEndDate: '2026-06-30',
    color: '#FF0000',
    ...overrides
})

const event = (overrides: Partial<EventWithOwnerDto> = {}) =>
    new EventWithOwnerDto({ id: 7, title: 'Dentist', startDateTime: new Date(2026, 2, 10, 9, 30), ...overrides })

const sentCreate = () => api.eventsPOST.mock.calls[0][0] as CreateEventDto
const sentUpdate = () => api.eventsPUT.mock.calls[0][1] as UpdateEventDto
const sentEdit = () => api.editOccurrence.mock.calls[0][1] as EditOccurrenceDto

let ops: ReturnType<typeof useEventOperations>

beforeEach(() => {
    vi.spyOn(console, 'log').mockImplementation(() => {}) // deleteEvent logs debug output
    ops = useEventOperations()
})

describe('createEvent', () => {
    it('combines the selected day with the form times', async () => {
        await ops.createEvent(form(), selectedDate)

        const dto = sentCreate()
        expect(dto).toBeInstanceOf(CreateEventDto)
        expect(dto.startDateTime).toEqual(new Date(2026, 2, 10, 9, 30))
        expect(dto.endDateTime).toEqual(new Date(2026, 2, 10, 10, 45))
        expect(dto.isAllDay).toBe(false)
        expect(dto.title).toBe('Dentist')
        expect(dto.color).toBe('#FF0000')
    })

    it('starts an all-day event at midnight without an end time', async () => {
        await ops.createEvent(form({ isAllDay: true }), selectedDate)

        expect(sentCreate().startDateTime).toEqual(new Date(2026, 2, 10))
        expect(sentCreate().endDateTime).toBeUndefined()
        expect(sentCreate().isAllDay).toBe(true)
    })

    it('leaves the end empty when the form has no end time', async () => {
        await ops.createEvent(form({ endTime: '' }), selectedDate)

        expect(sentCreate().endDateTime).toBeUndefined()
    })

    it('sends no recurrence fields for a one-off event, even if the form still holds them', async () => {
        await ops.createEvent(form({ isRecurring: false }), selectedDate)

        const dto = sentCreate()
        expect(dto.isRecurring).toBe(false)
        expect(dto.recurrencePattern).toBeUndefined()
        expect(dto.recurrenceInterval).toBeUndefined()
        expect(dto.recurrenceEndDate).toBeUndefined()
        expect(dto.recurrenceRule).toBeUndefined()
    })

    it('sends pattern, interval and end date for a simple recurring event', async () => {
        await ops.createEvent(form({ isRecurring: true }), selectedDate)

        const dto = sentCreate()
        expect(dto.isRecurring).toBe(true)
        expect(dto.recurrencePattern).toBe('weekly')
        expect(dto.recurrenceInterval).toBe(2)
        expect(dto.recurrenceEndDate).toEqual(new Date('2026-06-30'))
        expect(dto.recurrenceRule).toBeUndefined()
    })

    it('sends the RRULE instead of pattern and interval when there is one', async () => {
        await ops.createEvent(form({ isRecurring: true, recurrenceRule: 'FREQ=WEEKLY;BYDAY=MO,WE' }), selectedDate)

        const dto = sentCreate()
        expect(dto.recurrenceRule).toBe('FREQ=WEEKLY;BYDAY=MO,WE')
        expect(dto.recurrencePattern).toBeUndefined()
        expect(dto.recurrenceInterval).toBeUndefined()
    })

    it('omits an empty description', async () => {
        await ops.createEvent(form({ description: '' }), selectedDate)

        expect(sentCreate().description).toBeUndefined()
    })

    it('returns what the API returns', async () => {
        api.eventsPOST.mockResolvedValue({ id: 99 })

        await expect(ops.createEvent(form(), selectedDate)).resolves.toEqual({ id: 99 })
    })
})

describe('updateEvent', () => {
    it('puts the form to the event id and keeps the parent link', async () => {
        await ops.updateEvent(event({ parentEventId: 3 }), form({ startTime: '14:00', endTime: '15:00' }), selectedDate)

        expect(api.eventsPUT).toHaveBeenCalledWith(7, expect.any(UpdateEventDto))
        expect(sentUpdate().startDateTime).toEqual(new Date(2026, 2, 10, 14, 0))
        expect(sentUpdate().endDateTime).toEqual(new Date(2026, 2, 10, 15, 0))
        expect(sentUpdate().parentEventId).toBe(3)
    })

    it('refuses an event without id', async () => {
        await expect(ops.updateEvent(event({ id: undefined }), form(), selectedDate)).rejects.toThrow('Event ID is required')
        expect(api.eventsPUT).not.toHaveBeenCalled()
    })
})

describe('deleteEvent', () => {
    it('hides only the clicked occurrence of a series by adding an exception', async () => {
        const occurrence = event({ isRecurring: true, startDateTime: new Date(2026, 2, 17, 9, 30) })

        await ops.deleteEvent(occurrence, false)

        expect(api.addException).toHaveBeenCalledWith(7, new Date(2026, 2, 17, 9, 30))
        expect(api.eventsDELETE).not.toHaveBeenCalled()
    })

    it('treats an event with only an RRULE as recurring too', async () => {
        await ops.deleteEvent(event({ isRecurring: false, recurrenceRule: 'FREQ=DAILY' }), false)

        expect(api.addException).toHaveBeenCalled()
        expect(api.eventsDELETE).not.toHaveBeenCalled()
    })

    it('deletes the whole series when asked to', async () => {
        await ops.deleteEvent(event({ isRecurring: true }), true)

        expect(api.eventsDELETE).toHaveBeenCalledWith(7)
        expect(api.addException).not.toHaveBeenCalled()
    })

    it('deletes a one-off event', async () => {
        await ops.deleteEvent(event({ isRecurring: false }))

        expect(api.eventsDELETE).toHaveBeenCalledWith(7)
    })

    it('never deletes the series when a single occurrence has no start date', async () => {
        await ops.deleteEvent(event({ isRecurring: true, startDateTime: undefined }), false)

        expect(api.eventsDELETE).not.toHaveBeenCalled()
        expect(api.addException).not.toHaveBeenCalled()
    })

    it('refuses an event without id', async () => {
        await expect(ops.deleteEvent(event({ id: undefined }))).rejects.toThrow('Event ID is required')
    })
})

describe('editOccurrence', () => {
    it('sends the original occurrence date and the new times', async () => {
        const occurrence = event({ isRecurring: true, startDateTime: new Date(2026, 2, 17, 9, 30) })

        await ops.editOccurrence(occurrence, form({ title: 'Moved', startTime: '16:00', endTime: '17:00' }), dayjs(new Date(2026, 2, 18)))

        expect(api.editOccurrence).toHaveBeenCalledWith(7, expect.any(EditOccurrenceDto))
        expect(sentEdit().originalOccurrenceDate).toEqual(new Date(2026, 2, 17, 9, 30))
        expect(sentEdit().newStartDateTime).toEqual(new Date(2026, 2, 18, 16, 0))
        expect(sentEdit().newEndDateTime).toEqual(new Date(2026, 2, 18, 17, 0))
        expect(sentEdit().title).toBe('Moved')
    })

    it('sends no end when the form has no end time', async () => {
        // The API's EditOccurrenceDto.NewEndDateTime is a non-nullable DateTime, so the server
        // falls back to DateTime.MinValue here; this test pins what the frontend sends.
        await ops.editOccurrence(event({ isRecurring: true }), form({ endTime: '' }), selectedDate)

        expect(sentEdit().newEndDateTime).toBeUndefined()
    })

    it('refuses an occurrence without id or start date', async () => {
        await expect(ops.editOccurrence(event({ id: undefined }), form(), selectedDate)).rejects.toThrow()
        await expect(ops.editOccurrence(event({ startDateTime: undefined }), form(), selectedDate)).rejects.toThrow()
        expect(api.editOccurrence).not.toHaveBeenCalled()
    })
})

describe('helpers', () => {
    it.each([
        [{ isRecurring: true }, true],
        [{ isRecurring: false, recurrenceRule: 'FREQ=DAILY' }, true],
        [{ isRecurring: false }, false]
    ])('isRecurringEvent(%o) is %s', (fields, expected) => {
        expect(ops.isRecurringEvent(event(fields))).toBe(expected)
    })

    it('confirmDelete asks the user', () => {
        const confirm = vi.fn().mockReturnValue(true)
        vi.stubGlobal('confirm', confirm)

        expect(ops.confirmDelete(event())).toBe(true)
        expect(confirm).toHaveBeenCalledWith('Are you sure you want to delete this event?')
    })

    it('formatErrorMessage lists validation errors per field', () => {
        const message = ops.formatErrorMessage({ response: { errors: { Title: ['is required', 'too short'], Color: 'invalid' } } })

        expect(message).toBe('Validation errors:\nTitle: is required, too short\nColor: invalid')
    })

    it.each([
        [{ response: { title: 'One or more errors occurred.' } }, 'One or more errors occurred.'],
        [{ response: { data: { message: 'Event not found' } } }, 'Event not found'],
        [{}, 'Failed to save event. Please try again.']
    ])('formatErrorMessage(%o) is %s', (error, expected) => {
        expect(ops.formatErrorMessage(error)).toBe(expected)
    })
})
