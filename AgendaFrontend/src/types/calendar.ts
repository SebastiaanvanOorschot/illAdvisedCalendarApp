export type CalendarDate = {
  date: number;
  thisMonth: boolean;
  day?: number;      // present from MonthView/WeekView (grid), absent from ListView
  month?: number;     // present from ListView, absent from grid views
  year?: number;      // idem
};
