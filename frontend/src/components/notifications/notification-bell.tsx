'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import { BellIcon, CheckIcon, TrashIcon } from '@heroicons/react/24/outline';
import { BellAlertIcon } from '@heroicons/react/24/solid';
import { apiClient } from '@/lib/api-client';
import { useAuth } from '@/contexts/auth-context';
import { cn } from '@/lib/utils';
import type { NotificationDto } from '@/types/notifications';

const POLL_INTERVAL = 30_000; // 30 seconds

export function NotificationBell() {
  const { isAuthenticated } = useAuth();
  const t = useTranslations('notifications');
  const router = useRouter();

  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<NotificationDto[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  // Poll for unread count
  const fetchUnreadCount = useCallback(async () => {
    if (!isAuthenticated) return;
    try {
      const result = await apiClient.getNotificationUnreadCount();
      setUnreadCount(result.count);
    } catch {
      // silently ignore polling errors
    }
  }, [isAuthenticated]);

  useEffect(() => {
    fetchUnreadCount();
    const interval = setInterval(fetchUnreadCount, POLL_INTERVAL);
    return () => clearInterval(interval);
  }, [fetchUnreadCount]);

  // Fetch notifications when panel opens
  const fetchNotifications = useCallback(async () => {
    if (!isAuthenticated) return;
    setIsLoading(true);
    try {
      const result = await apiClient.getNotifications({ pageSize: 15 });
      setNotifications(result.items);
    } catch {
      // silently ignore
    } finally {
      setIsLoading(false);
    }
  }, [isAuthenticated]);

  useEffect(() => {
    if (isOpen) {
      fetchNotifications();
    }
  }, [isOpen, fetchNotifications]);

  // Close panel on click outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        panelRef.current &&
        !panelRef.current.contains(event.target as Node) &&
        buttonRef.current &&
        !buttonRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };

    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isOpen]);

  const handleMarkAsRead = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const originalNotifications = [...notifications];
    const originalUnreadCount = unreadCount;

    // Optimistic update
    setNotifications((prev) =>
      prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))
    );
    setUnreadCount((prev) => Math.max(0, prev - 1));

    try {
      await apiClient.markNotificationRead(id);
    } catch (error) {
      console.error('Failed to mark notification as read:', error);
      // Revert on error
      setNotifications(originalNotifications);
      setUnreadCount(originalUnreadCount);
    }
  };

  const handleMarkAllRead = async () => {
    const originalNotifications = [...notifications];
    const originalUnreadCount = unreadCount;

    // Optimistic update
    setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
    setUnreadCount(0);

    try {
      await apiClient.markAllNotificationsRead();
    } catch (error) {
      console.error('Failed to mark all notifications as read:', error);
      // Revert on error
      setNotifications(originalNotifications);
      setUnreadCount(originalUnreadCount);
    }
  };

  const handleDelete = async (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    const originalNotifications = [...notifications];
    const originalUnreadCount = unreadCount;
    const deleted = notifications.find((n) => n.id === id);

    // Optimistic update
    setNotifications((prev) => prev.filter((n) => n.id !== id));
    if (deleted && !deleted.isRead) {
      setUnreadCount((prev) => Math.max(0, prev - 1));
    }

    try {
      await apiClient.deleteNotification(id);
    } catch (error) {
      console.error('Failed to delete notification:', error);
      // Revert on error
      setNotifications(originalNotifications);
      setUnreadCount(originalUnreadCount);
    }
  };

  const handleNotificationClick = (notification: NotificationDto) => {
    // Mark as read
    if (!notification.isRead) {
      apiClient.markNotificationRead(notification.id).catch(() => {});
      setNotifications((prev) =>
        prev.map((n) =>
          n.id === notification.id ? { ...n, isRead: true } : n
        )
      );
      setUnreadCount((prev) => Math.max(0, prev - 1));
    }

    // Deep linking via data payload — only allow internal paths (starting with '/')
    if (notification.data) {
      try {
        const data = JSON.parse(notification.data);
        if (data.href && typeof data.href === 'string' && data.href.startsWith('/')) {
          setIsOpen(false);
          router.push(data.href);
          return;
        }
      } catch {
        // ignore parse errors
      }
    }

    setIsOpen(false);
  };

  const formatTime = (dateStr: string) => {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMinutes = Math.floor(diffMs / 60_000);

    if (diffMinutes < 1) return t('justNow');
    if (diffMinutes < 60) return t('minutesAgo', { count: diffMinutes });

    const diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) return t('hoursAgo', { count: diffHours });

    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) return t('daysAgo', { count: diffDays });

    return date.toLocaleDateString();
  };

  if (!isAuthenticated) return null;

  return (
    <div className="relative">
      <button
        ref={buttonRef}
        onClick={() => setIsOpen(!isOpen)}
        className="relative p-2 rounded-xl text-ink-500 hover:text-primary-600 hover:bg-ink-100 transition-colors cursor-pointer"
        aria-label={t('title')}
      >
        {unreadCount > 0 ? (
          <BellAlertIcon className="w-5 h-5" />
        ) : (
          <BellIcon className="w-5 h-5" />
        )}
        {unreadCount > 0 && (
          <span className="absolute -top-0.5 -right-0.5 flex items-center justify-center min-w-[18px] h-[18px] px-1 rounded-full bg-danger text-white text-[10px] font-bold leading-none">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {isOpen && (
        <div
          ref={panelRef}
          className="absolute right-0 top-full mt-2 w-80 sm:w-96 max-h-[70vh] bg-white rounded-xl border border-ink-200 shadow-xl z-50 flex flex-col overflow-hidden"
        >
          {/* Header */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-ink-100">
            <h3 className="text-sm font-semibold text-ink-900">
              {t('title')}
            </h3>
            {unreadCount > 0 && (
              <button
                onClick={handleMarkAllRead}
                className="text-xs text-primary-600 hover:text-primary-700 font-medium cursor-pointer"
              >
                {t('markAllRead')}
              </button>
            )}
          </div>

          {/* Notification list */}
          <div className="flex-1 overflow-y-auto">
            {isLoading ? (
              <div className="py-8 text-center text-ink-400 text-sm">
                {t('loading')}
              </div>
            ) : notifications.length === 0 ? (
              <div className="py-8 text-center text-ink-400 text-sm">
                {t('empty')}
              </div>
            ) : (
              <div>
                {notifications.map((notification) => (
                  <div
                    key={notification.id}
                    className={cn(
                      'flex items-start border-b border-ink-50 transition-colors hover:bg-ink-50',
                      !notification.isRead && 'bg-primary-50/50'
                    )}
                  >
                    {/* Row button — keyboard-accessible, covers dot + content */}
                    <button
                      type="button"
                      onClick={() => handleNotificationClick(notification)}
                      className="flex items-start gap-3 px-4 py-3 flex-1 min-w-0 text-left cursor-pointer"
                      aria-label={notification.title}
                    >
                      {/* Unread dot */}
                      <div className="pt-1.5 shrink-0">
                        <div
                          className={cn(
                            'w-2 h-2 rounded-full',
                            notification.isRead
                              ? 'bg-transparent'
                              : 'bg-primary-500'
                          )}
                        />
                      </div>

                      {/* Content */}
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-ink-900 truncate">
                          {notification.title}
                        </p>
                        <p className="text-xs text-ink-500 mt-0.5 line-clamp-2">
                          {notification.body}
                        </p>
                        <p className="text-[11px] text-ink-400 mt-1">
                          {formatTime(notification.createdAt)}
                        </p>
                      </div>
                    </button>

                    {/* Actions — sibling of row button to avoid nested interactive elements */}
                    <div className="flex items-center gap-1 shrink-0 pt-4 pr-4">
                      {!notification.isRead && (
                        <button
                          type="button"
                          onClick={(e) => handleMarkAsRead(notification.id, e)}
                          className="p-1 rounded-md hover:bg-ink-100 text-ink-400 hover:text-primary-600 cursor-pointer"
                          aria-label={t('markRead')}
                          title={t('markRead')}
                        >
                          <CheckIcon className="w-3.5 h-3.5" />
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={(e) => handleDelete(notification.id, e)}
                        className="p-1 rounded-md hover:bg-ink-100 text-ink-400 hover:text-danger cursor-pointer"
                        aria-label={t('dismiss')}
                        title={t('dismiss')}
                      >
                        <TrashIcon className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
