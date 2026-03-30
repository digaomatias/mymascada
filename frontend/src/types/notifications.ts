export interface NotificationDto {
  id: string;
  type: string;
  priority: string;
  title: string;
  body: string;
  data: string | null;
  isRead: boolean;
  createdAt: string;
  readAt: string | null;
}

export interface NotificationListResponse {
  items: NotificationDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface UnreadCountResponse {
  count: number;
}

export interface NotificationPreferenceDto {
  channelPreferences: string | null;
  quietHoursStart: string | null;
  quietHoursEnd: string | null;
  quietHoursTimezone: string | null;
  largeTransactionThreshold: number | null;
  budgetAlertPercentage: number | null;
  runwayWarningMonths: number | null;
}

export interface UpdateNotificationPreferenceRequest {
  channelPreferences?: string | null;
  quietHoursStart?: string | null;
  quietHoursEnd?: string | null;
  quietHoursTimezone?: string | null;
  largeTransactionThreshold?: number | null;
  budgetAlertPercentage?: number | null;
  runwayWarningMonths?: number | null;
}
