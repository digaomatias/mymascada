export interface ChatMessageDto {
  id: number;
  role: 'user' | 'assistant';
  content: string;
  createdAt: string;
}

export interface SendChatMessageResponse {
  success: boolean;
  userMessage?: ChatMessageDto;
  assistantMessage?: ChatMessageDto;
  error?: string;
}

export interface ChatHistoryResponse {
  messages: ChatMessageDto[];
  hasMore: boolean;
}
