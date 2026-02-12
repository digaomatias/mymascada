export interface AiSettingsResponse {
  providerType: string;
  providerName: string;
  modelId: string;
  apiEndpoint?: string;
  hasApiKey: boolean;
  apiKeyLastFour?: string;
  isValidated: boolean;
  lastValidatedAt?: string;
}

export interface AiSettingsRequest {
  providerType: string;
  providerName: string;
  apiKey?: string;
  modelId: string;
  apiEndpoint?: string;
}

export interface AiConnectionTestRequest {
  providerType: string;
  apiKey: string;
  modelId: string;
  apiEndpoint?: string;
}

export interface AiConnectionTestResult {
  success: boolean;
  latencyMs?: number;
  modelResponse?: string;
  error?: string;
}

export interface AiProviderPreset {
  id: string;
  name: string;
  providerType: string;
  defaultEndpoint?: string;
  models: AiModelPreset[];
}

export interface AiModelPreset {
  id: string;
  name: string;
}
