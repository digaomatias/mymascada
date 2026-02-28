'use client';

import { useState, useEffect, useRef, useCallback, KeyboardEvent } from 'react';
import { useTranslations } from 'next-intl';
import { AppLayout } from '@/components/app-layout';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { apiClient } from '@/lib/api-client';
import ReactMarkdown from 'react-markdown';
import { toast } from 'sonner';
import Link from 'next/link';
import {
  PaperAirplaneIcon,
  TrashIcon,
  SparklesIcon,
  ExclamationCircleIcon,
  CogIcon,
} from '@heroicons/react/24/outline';
import type { ChatMessageDto } from '@/types/chat';
import type { Components } from 'react-markdown';
import { useAuthGuard } from '@/hooks/use-auth-guard';
import { ChatSkeleton } from '@/components/skeletons';

const MAX_CHARS = 5000;

// Custom components for chat-friendly markdown rendering
const chatComponents: Partial<Components> = {
  // Headings render as compact section labels with a subtle separator
  h1: ({ children }) => (
    <div className="text-xs font-semibold text-violet-700 mt-3 first:mt-0 mb-1 pb-0.5 border-b border-slate-100">
      {children}
    </div>
  ),
  h2: ({ children }) => (
    <div className="text-xs font-semibold text-violet-700 mt-3 first:mt-0 mb-1 pb-0.5 border-b border-slate-100">
      {children}
    </div>
  ),
  h3: ({ children }) => (
    <div className="text-xs font-semibold text-violet-700 mt-3 first:mt-0 mb-1 pb-0.5 border-b border-slate-100">
      {children}
    </div>
  ),
  // Colorize financial amounts: only explicitly signed amounts get color
  // -$... → red, +$... → green, plain $... or ~$... → neutral accent
  strong: ({ children }) => {
    const text = String(children ?? '');
    if (/^[~+-]?\$/.test(text) || /^[~+-]?\d/.test(text)) {
      const color = text.startsWith('-')
        ? 'text-red-600'
        : text.startsWith('+')
          ? 'text-emerald-600'
          : 'text-violet-700';
      return <strong className={`font-semibold ${color}`}>{children}</strong>;
    }
    return <strong className="font-semibold text-slate-900">{children}</strong>;
  },
};

export default function ChatPage() {
  const { shouldRender, isAuthResolved } = useAuthGuard();
  const t = useTranslations('chat');
  const tCommon = useTranslations('common');

  // State
  const [messages, setMessages] = useState<ChatMessageDto[]>([]);
  const [input, setInput] = useState('');
  const [sending, setSending] = useState(false);
  const [loadingHistory, setLoadingHistory] = useState(true);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [chatConfigured, setChatConfigured] = useState<boolean | null>(null);
  const [showClearConfirm, setShowClearConfirm] = useState(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const messagesContainerRef = useRef<HTMLDivElement>(null);
  const shouldAutoScroll = useRef(true);

  // Check chat AI configuration
  const checkChatConfiguration = useCallback(async () => {
    try {
      const settings = await apiClient.getAiSettings('chat');
      setChatConfigured(settings?.hasSettings === true);
    } catch {
      setChatConfigured(false);
    }
  }, []);

  // Load initial history
  const loadHistory = useCallback(async () => {
    try {
      setLoadingHistory(true);
      const data = await apiClient.getChatHistory(50);
      setMessages(data.messages);
      setHasMore(data.hasMore);
    } catch (error) {
      console.error('Failed to load chat history:', error);
      toast.error(t('errors.loadFailed'));
    } finally {
      setLoadingHistory(false);
    }
  }, [t]);

  useEffect(() => {
    if (isAuthResolved) {
      checkChatConfiguration();
      loadHistory();
    }
  }, [isAuthResolved, checkChatConfiguration, loadHistory]);

  // Auto-scroll to bottom when new messages arrive
  useEffect(() => {
    if (shouldAutoScroll.current && messagesEndRef.current) {
      messagesEndRef.current.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages, sending]);

  // Auto-resize textarea
  const adjustTextareaHeight = useCallback(() => {
    const textarea = textareaRef.current;
    if (textarea) {
      textarea.style.height = 'auto';
      const maxHeight = 150;
      textarea.style.height = `${Math.min(textarea.scrollHeight, maxHeight)}px`;
    }
  }, []);

  useEffect(() => {
    adjustTextareaHeight();
  }, [input, adjustTextareaHeight]);

  // Send message handler
  const handleSend = async (content?: string) => {
    const messageContent = content || input.trim();
    if (!messageContent || sending) return;

    setSending(true);
    shouldAutoScroll.current = true;
    setInput('');

    // Optimistically add user message
    const tempUserMessage: ChatMessageDto = {
      id: -Date.now(),
      role: 'user',
      content: messageContent,
      createdAt: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, tempUserMessage]);

    try {
      const response = await apiClient.sendChatMessage(messageContent);

      if (response.success && response.userMessage && response.assistantMessage) {
        // Replace the temp message with the real ones
        setMessages((prev) => {
          const filtered = prev.filter((m) => m.id !== tempUserMessage.id);
          return [...filtered, response.userMessage!, response.assistantMessage!];
        });
      } else {
        // Error from AI - show error message in a bubble
        const errorMessage: ChatMessageDto = {
          id: -(Date.now() + 1),
          role: 'assistant',
          content: response.error || t('errors.sendFailed'),
          createdAt: new Date().toISOString(),
        };
        setMessages((prev) => [...prev, errorMessage]);
      }
    } catch (error) {
      console.error('Failed to send message:', error);
      toast.error(t('errors.sendFailed'));
      // Remove the optimistic message on total failure
      setMessages((prev) => prev.filter((m) => m.id !== tempUserMessage.id));
      setInput(messageContent);
    } finally {
      setSending(false);
      textareaRef.current?.focus();
    }
  };

  // Load more handler
  const handleLoadMore = async () => {
    if (loadingMore || !hasMore || messages.length === 0) return;

    setLoadingMore(true);
    shouldAutoScroll.current = false;

    try {
      const oldestId = Math.min(...messages.filter((m) => m.id > 0).map((m) => m.id));
      const data = await apiClient.getChatHistory(50, oldestId);
      setMessages((prev) => [...data.messages, ...prev]);
      setHasMore(data.hasMore);
    } catch (error) {
      console.error('Failed to load more messages:', error);
      toast.error(t('errors.loadFailed'));
    } finally {
      setLoadingMore(false);
    }
  };

  // Clear history handler
  const handleClearHistory = async () => {
    try {
      await apiClient.clearChatHistory();
      setMessages([]);
      setHasMore(false);
      toast.success(t('cleared'));
    } catch (error) {
      console.error('Failed to clear conversation:', error);
      toast.error(t('errors.clearFailed'));
    } finally {
      setShowClearConfirm(false);
    }
  };

  // Keyboard handlers
  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  // Handle suggested prompt click
  const handleSuggestedPrompt = (prompt: string) => {
    handleSend(prompt);
  };

  const formatTimestamp = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };

  if (!shouldRender) return null;

  // Not Configured State
  if (chatConfigured === false) {
    return (
      <AppLayout mainClassName="relative z-10 flex-1 flex items-center justify-center p-4 pb-24 md:pb-4">
          <div className="max-w-md w-full rounded-[26px] border border-violet-100/70 bg-white/92 shadow-[0_20px_46px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs p-8 text-center">
            <div className="w-16 h-16 bg-amber-100 rounded-2xl flex items-center justify-center mx-auto mb-4">
              <ExclamationCircleIcon className="w-8 h-8 text-amber-600" />
            </div>
            <h2 className="text-xl font-semibold text-slate-900 mb-2">
              {t('notConfigured')}
            </h2>
            <p className="text-slate-500 mb-6">
              {t('notConfiguredDescription')}
            </p>
            <Link href="/settings/ai">
              <Button variant="primary">
                <CogIcon className="w-4 h-4 mr-2" />
                {t('configureButton')}
              </Button>
            </Link>
          </div>
      </AppLayout>
    );
  }

  // Show skeleton while auth or chat config is still loading
  if (!isAuthResolved || chatConfigured === null) {
    return (
      <AppLayout mainClassName="relative z-10 flex-1 flex flex-col max-w-4xl mx-auto w-full pb-24 md:pb-0" noBackground>
        <ChatSkeleton />
      </AppLayout>
    );
  }

  const hasMessages = messages.length > 0;

  return (
    <AppLayout mainClassName="relative z-10 flex-1 flex flex-col max-w-4xl mx-auto w-full pb-24 md:pb-0" noBackground>
        {/* Header bar with clear button */}
        {hasMessages && (
          <div className="flex items-center justify-between px-4 py-2 border-b border-slate-200 bg-white/90 backdrop-blur-xs">
            <div className="flex items-center gap-2">
              <SparklesIcon className="w-5 h-5 text-violet-600" />
              <h1 className="text-sm font-medium text-slate-700">{t('title')}</h1>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowClearConfirm(true)}
              className="text-slate-500 hover:text-red-600"
            >
              <TrashIcon className="w-4 h-4 mr-1" />
              {t('clearHistory')}
            </Button>
          </div>
        )}

        {/* Messages area */}
        <div
          ref={messagesContainerRef}
          className="flex-1 overflow-y-auto px-4 py-4 space-y-4"
        >
          {loadingHistory ? (
            <div className="flex items-center justify-center py-12">
              <div className="text-center">
                <div className="animate-spin w-8 h-8 border-2 border-violet-600 border-t-transparent rounded-full mx-auto mb-3" />
                <p className="text-sm text-slate-500">{t('loading')}</p>
              </div>
            </div>
          ) : !hasMessages ? (
            /* Empty State */
            <div className="flex-1 flex items-center justify-center py-12">
              <div className="max-w-md w-full text-center">
                <div className="w-20 h-20 bg-gradient-to-br from-violet-500 to-violet-600 rounded-2xl flex items-center justify-center mx-auto mb-6 shadow-lg">
                  <SparklesIcon className="w-10 h-10 text-white" />
                </div>
                <h2 className="text-2xl font-bold text-slate-900 mb-2">
                  {t('title')}
                </h2>
                <p className="text-slate-500 mb-8">
                  {t('subtitle')}
                </p>
                <div className="flex flex-wrap gap-2 justify-center">
                  {(['spending', 'categories', 'budgets', 'saving', 'summary'] as const).map((key) => (
                    <button
                      key={key}
                      onClick={() => handleSuggestedPrompt(t(`suggestedPrompts.${key}`))}
                      disabled={sending}
                      className="px-4 py-2 text-sm bg-white border border-violet-200/60 rounded-full text-slate-700 hover:bg-violet-50 hover:border-violet-300 hover:text-violet-700 transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      {t(`suggestedPrompts.${key}`)}
                    </button>
                  ))}
                </div>
              </div>
            </div>
          ) : (
            <>
              {/* Load More */}
              {hasMore && (
                <div className="text-center py-2">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleLoadMore}
                    loading={loadingMore}
                    disabled={loadingMore}
                  >
                    {loadingMore ? t('loading') : t('loadMore')}
                  </Button>
                </div>
              )}

              {/* Message Bubbles */}
              {messages.map((message) => (
                <div
                  key={message.id}
                  className={`flex ${message.role === 'user' ? 'justify-end' : 'justify-start'}`}
                >
                  <div
                    className={`max-w-[85%] sm:max-w-[75%] rounded-2xl px-4 py-3 ${
                      message.role === 'user'
                        ? 'bg-violet-600 text-white'
                        : message.id < 0 && !message.content
                          ? ''
                          : 'bg-white border border-slate-200 text-slate-800'
                    }`}
                  >
                    {message.role === 'assistant' ? (
                      <div className="prose prose-sm max-w-none break-words overflow-hidden prose-p:my-1 prose-ul:my-1 prose-ol:my-1 prose-li:my-0.5 prose-pre:my-2 prose-pre:overflow-x-auto prose-pre:max-w-full prose-table:overflow-x-auto prose-table:block prose-table:text-xs prose-code:text-violet-700 prose-code:bg-violet-50 prose-code:px-1 prose-code:rounded prose-code:break-all">
                        <ReactMarkdown components={chatComponents}>{message.content}</ReactMarkdown>
                      </div>
                    ) : (
                      <p className="whitespace-pre-wrap break-words">{message.content}</p>
                    )}
                    <p
                      className={`text-xs mt-1.5 ${
                        message.role === 'user' ? 'text-violet-200' : 'text-slate-400'
                      }`}
                    >
                      {formatTimestamp(message.createdAt)}
                    </p>
                  </div>
                </div>
              ))}

              {/* Typing indicator */}
              {sending && (
                <div className="flex justify-start">
                  <div className="bg-white border border-slate-200 rounded-2xl px-4 py-3">
                    <div className="flex items-center gap-1.5">
                      <div className="w-2 h-2 bg-slate-400 rounded-full animate-bounce" style={{ animationDelay: '0ms' }} />
                      <div className="w-2 h-2 bg-slate-400 rounded-full animate-bounce" style={{ animationDelay: '150ms' }} />
                      <div className="w-2 h-2 bg-slate-400 rounded-full animate-bounce" style={{ animationDelay: '300ms' }} />
                    </div>
                  </div>
                </div>
              )}
            </>
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Input area */}
        <div className="border-t border-slate-200 bg-white px-4 py-3">
          <div className="flex items-end gap-2">
            <div className="flex-1 relative">
              <textarea
                ref={textareaRef}
                value={input}
                onChange={(e) => {
                  if (e.target.value.length <= MAX_CHARS) {
                    setInput(e.target.value);
                  }
                }}
                onKeyDown={handleKeyDown}
                placeholder={t('inputPlaceholder')}
                disabled={sending}
                rows={1}
                className="w-full resize-none rounded-xl border border-slate-300 px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-violet-500 focus:border-transparent disabled:opacity-50 disabled:cursor-not-allowed"
                style={{ maxHeight: '150px' }}
              />
              {input.length > MAX_CHARS * 0.9 && (
                <span
                  className={`absolute bottom-1 right-2 text-xs ${
                    input.length >= MAX_CHARS ? 'text-red-500' : 'text-slate-400'
                  }`}
                >
                  {input.length}/{MAX_CHARS}
                </span>
              )}
            </div>
            <Button
              variant="primary"
              size="icon"
              onClick={() => handleSend()}
              disabled={sending || !input.trim()}
              className="shrink-0 w-10 h-10 rounded-xl"
            >
              <PaperAirplaneIcon className="w-5 h-5" />
            </Button>
          </div>
        </div>
      <ConfirmationDialog
        isOpen={showClearConfirm}
        onClose={() => setShowClearConfirm(false)}
        onConfirm={handleClearHistory}
        title={t('clearConfirmTitle')}
        description={t('clearConfirmMessage')}
        confirmText={tCommon('confirm')}
        cancelText={tCommon('cancel')}
        variant="danger"
      />
    </AppLayout>
  );
}

export const dynamic = 'force-dynamic';
