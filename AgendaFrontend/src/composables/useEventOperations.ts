import dayjs from 'dayjs';
import { AgendaAPI, Event } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

export interface EventFormData {
    title: string;
    description: string;
    startTime: string;
    endTime: string;
    isRecurring: boolean;
    recurrencePattern: string;
    recurrenceInterval: number;
    recurrenceEndDate: string;
    recurrenceRule?: string;
    color: string;
}

export function useEventOperations() {
    /**
     * Create a new event
     */
    async function createEvent(formData: EventFormData, selectedDate: dayjs.Dayjs): Promise<Event> {
        const startDateTime = selectedDate
            .hour(parseInt(formData.startTime.split(':')[0]))
            .minute(parseInt(formData.startTime.split(':')[1]))
            .second(0)
            .millisecond(0)
            .toDate();

        const endDateTime = selectedDate
            .hour(parseInt(formData.endTime.split(':')[0]))
            .minute(parseInt(formData.endTime.split(':')[1]))
            .second(0)
            .millisecond(0)
            .toDate();

        const newEvent = new Event({
            title: formData.title,
            description: formData.description || undefined,
            startDateTime: startDateTime,
            endDateTime: endDateTime,
            color: formData.color,
            isRecurring: formData.isRecurring || false,
            recurrencePattern: formData.isRecurring && !formData.recurrenceRule ? formData.recurrencePattern : undefined,
            recurrenceInterval: formData.isRecurring && !formData.recurrenceRule ? formData.recurrenceInterval : undefined,
            recurrenceRule: formData.recurrenceRule || undefined,
            recurrenceEndDate: formData.isRecurring && formData.recurrenceEndDate
                ? new Date(formData.recurrenceEndDate)
                : undefined
        });

        return await api.eventsPOST(newEvent);
    }

    /**
     * Update an existing event
     */
    async function updateEvent(
        eventToUpdate: Event,
        formData: EventFormData,
        selectedDate: dayjs.Dayjs
    ): Promise<void> {
        if (!eventToUpdate.id) {
            throw new Error('Event ID is required for update');
        }

        // Update times from form
        const startDateTime = selectedDate
            .hour(parseInt(formData.startTime.split(':')[0]))
            .minute(parseInt(formData.startTime.split(':')[1]))
            .second(0)
            .millisecond(0)
            .toDate();

        const endDateTime = selectedDate
            .hour(parseInt(formData.endTime.split(':')[0]))
            .minute(parseInt(formData.endTime.split(':')[1]))
            .second(0)
            .millisecond(0)
            .toDate();

        const updatedEvent = new Event({
            id: eventToUpdate.id,
            title: formData.title,
            description: formData.description || undefined,
            startDateTime: startDateTime,
            endDateTime: endDateTime,
            color: formData.color,
            isRecurring: formData.isRecurring || false,
            recurrencePattern: formData.isRecurring && !formData.recurrenceRule ? formData.recurrencePattern : undefined,
            recurrenceInterval: formData.isRecurring && !formData.recurrenceRule ? formData.recurrenceInterval : undefined,
            recurrenceRule: formData.recurrenceRule || undefined,
            recurrenceEndDate: formData.isRecurring && formData.recurrenceEndDate
                ? new Date(formData.recurrenceEndDate)
                : undefined,
            parentEventId: eventToUpdate.parentEventId,
            userId: eventToUpdate.userId,
            createdAt: eventToUpdate.createdAt,
            updatedAt: eventToUpdate.updatedAt
        });

        await api.eventsPUT(eventToUpdate.id, updatedEvent);
    }

    /**
     * Delete an event or event series
     * For single occurrences of recurring events, adds an exception date instead of deleting
     */
    async function deleteEvent(event: Event, deleteSeries: boolean = false): Promise<void> {
        if (!event.id) {
            throw new Error('Event ID is required for deletion');
        }

        const isRecurring = event.isRecurring || !!event.recurrenceRule;

        console.log('Delete Event Debug:', {
            eventId: event.id,
            isRecurring,
            deleteSeries,
            startDateTime: event.startDateTime,
            recurrenceRule: event.recurrenceRule
        });

        if (isRecurring && !deleteSeries) {
            // For single occurrence deletion, add an exception date to the parent event
            // The occurrence will be filtered out by the RecurrenceService
            if (event.startDateTime) {
                console.log('Adding exception for occurrence:', event.startDateTime);
                await api.addException(event.id, event.startDateTime);
                console.log('Exception added successfully');
            }
        } else if (deleteSeries && isRecurring) {
            // Delete the entire series
            console.log('Deleting entire series');
            await api.eventsDELETE(event.id);
        } else {
            // Delete single non-recurring event
            console.log('Deleting single non-recurring event');
            await api.eventsDELETE(event.id);
        }
    }

    /**
     * Edit a single occurrence of a recurring event
     * Creates a new event for the modified occurrence and adds an exception to the parent series
     */
    async function editOccurrence(
        event: Event,
        formData: EventFormData,
        selectedDate: dayjs.Dayjs
    ): Promise<void> {
        if (!event.id || !event.startDateTime) {
            throw new Error('Event ID and start date are required');
        }

        const startDateTime = selectedDate
            .hour(parseInt(formData.startTime.split(':')[0]))
            .minute(parseInt(formData.startTime.split(':')[1]))
            .second(0)
            .millisecond(0)
            .toDate();

        const endDateTime = selectedDate
            .hour(parseInt(formData.endTime.split(':')[0]))
            .minute(parseInt(formData.endTime.split(':')[1]))
            .second(0)
            .millisecond(0)
            .toDate();

        const request = {
            originalOccurrenceDate: event.startDateTime,
            title: formData.title,
            description: formData.description || undefined,
            newStartDateTime: startDateTime,
            newEndDateTime: endDateTime,
            color: formData.color
        };

        await api.editOccurrence(event.id, request);
    }

    /**
     * Check if an event is recurring
     */
    function isRecurringEvent(event: Event): boolean {
        return event.isRecurring || !!event.recurrenceRule;
    }

    /**
     * Prompt user for delete confirmation (non-recurring events only)
     */
    function confirmDelete(event: Event): boolean {
        return confirm('Are you sure you want to delete this event?');
    }

    /**
     * Format error response for display
     */
    function formatErrorMessage(error: any): string {
        let errorMessage = 'Failed to save event. Please try again.';

        if (error.response?.errors) {
            // Format validation errors
            const validationErrors = Object.entries(error.response.errors)
                .map(([field, messages]: [string, any]) => {
                    const errorList = Array.isArray(messages) ? messages : [messages];
                    return `${field}: ${errorList.join(', ')}`;
                })
                .join('\n');
            errorMessage = `Validation errors:\n${validationErrors}`;
        } else if (error.response?.title) {
            errorMessage = error.response.title;
        } else if (error.response?.data?.message) {
            errorMessage = error.response.data.message;
        }

        return errorMessage;
    }

    return {
        createEvent,
        updateEvent,
        deleteEvent,
        editOccurrence,
        confirmDelete,
        isRecurringEvent,
        formatErrorMessage
    };
}
