import { describe, expect, it } from 'vitest'
import { formatDate, formatTime } from '../dateFormat'

describe('formatTime', () => {
    it('shows 24-hour hours and minutes', () => {
        expect(formatTime(new Date(2026, 2, 10, 9, 5))).toBe('09:05')
        expect(formatTime(new Date(2026, 2, 10, 21, 30))).toBe('21:30')
    })

    it('is empty without a date', () => {
        expect(formatTime(undefined)).toBe('')
    })
})

describe('formatDate', () => {
    it('shows month name, day and year', () => {
        expect(formatDate(new Date(2026, 2, 1))).toBe('March 1, 2026')
    })

    it('is empty without a date', () => {
        expect(formatDate(undefined)).toBe('')
    })
})
