export interface TelegramSettingsResponse {
  hasSettings: boolean;
  botUsername?: string;
  isActive: boolean;
  isVerified: boolean;
  lastVerifiedAt?: string;
}

export interface SaveTelegramSettingsRequest {
  botToken: string;
}

export interface TelegramTestResult {
  success: boolean;
  botUsername?: string;
  botName?: string;
  error?: string;
}
