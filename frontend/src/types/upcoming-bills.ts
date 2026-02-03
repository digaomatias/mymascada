/**
 * Types for Upcoming Bills feature
 */

export interface UpcomingBillDto {
  merchantName: string;
  expectedAmount: number;
  expectedDate: string;
  daysUntilDue: number;
  confidenceScore: number; // 0.0-1.0
  confidenceLevel: 'High' | 'Medium';
  interval: 'Weekly' | 'Biweekly' | 'Monthly';
  occurrenceCount: number;
}

export interface UpcomingBillsResponse {
  bills: UpcomingBillDto[];
  totalBillsCount: number;
  totalExpectedAmount: number;
}
