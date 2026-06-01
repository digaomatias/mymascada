'use client';

import { useCallback, useEffect, useState } from 'react';
import { useTranslations } from 'next-intl';
import { toast } from 'sonner';
import { differenceInCalendarDays } from 'date-fns';
import {
  ExclamationTriangleIcon,
  XMarkIcon,
  ArrowTopRightOnSquareIcon,
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type {
  AkahuMigrationStatus,
  PendingMigrationConnection,
} from '@/types/bank-connections';

const DISMISS_KEY_PREFIX = 'akahu-migration-banner-dismissed-';

/**
 * Returns the dismiss-key for today (local time). The key rolls over each
 * calendar day so a dismissal silences the banner for the rest of the day
 * but it reappears tomorrow if the user still has pending connections.
 */
function todayDismissKey(now: Date = new Date()): string {
  const year = now.getFullYear().toString().padStart(4, '0');
  const month = (now.getMonth() + 1).toString().padStart(2, '0');
  const day = now.getDate().toString().padStart(2, '0');
  return `${DISMISS_KEY_PREFIX}${year}-${month}-${day}`;
}

function isDismissedToday(): boolean {
  if (typeof window === 'undefined') return false;
  try {
    return window.localStorage.getItem(todayDismissKey()) === 'true';
  } catch {
    return false;
  }
}

function setDismissedToday(): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(todayDismissKey(), 'true');
  } catch {
    // ignore — storage may be unavailable in some browsers
  }
}

function lastSyncedDays(connection: PendingMigrationConnection, now: Date): number | null {
  if (!connection.lastSyncedAt) return null;
  const parsed = new Date(connection.lastSyncedAt);
  if (Number.isNaN(parsed.getTime())) return null;
  const diff = differenceInCalendarDays(now, parsed);
  return diff >= 0 ? diff : 0;
}

export interface AkahuMigrationBannerProps {
  className?: string;
}

/**
 * Banner shown on the Bank Connections page (and optionally the dashboard)
 * when the user still has Akahu classic connections that need to be
 * re-authorised before the 24 May 2026 cut-over. Fails closed — renders
 * nothing on fetch errors or empty results.
 */
export function AkahuMigrationBanner({ className }: AkahuMigrationBannerProps) {
  const t = useTranslations('bankConnections.akahuMigration');
  const [status, setStatus] = useState<AkahuMigrationStatus | null>(null);
  const [dismissed, setDismissed] = useState(false);
  const [upgradingId, setUpgradingId] = useState<number | null>(null);
  const [isInitiatingAny, setIsInitiatingAny] = useState(false);

  // Read dismissal flag on mount. Done in useEffect so the initial server
  // render doesn't mismatch with the client (localStorage is client-only).
  useEffect(() => {
    setDismissed(isDismissedToday());
  }, []);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        const data = await apiClient.getAkahuMigrationStatus();
        if (!cancelled) {
          setStatus(data);
        }
      } catch (error) {
        // Quietly graceful — banner stays hidden on failure.
        console.warn('Failed to load Akahu migration status', error);
        if (!cancelled) {
          setStatus(null);
        }
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  const handleDismiss = useCallback(() => {
    setDismissedToday();
    setDismissed(true);
  }, []);

  const handleUpgrade = useCallback(async (connection: PendingMigrationConnection) => {
    // Defense-in-depth against rapid double-click before React re-renders disabled state.
    if (isInitiatingAny) return;
    setIsInitiatingAny(true);
    setUpgradingId(connection.connectionId);
    try {
      const result = await apiClient.initiateAkahuConnection();

      if (result.requiresCredentials) {
        toast.error(t('toasts.credentialsMissing'));
        return;
      }

      // Production App / OAuth mode — redirect to Akahu authorisation page.
      if (result.authorizationUrl) {
        if (typeof window !== 'undefined' && result.state) {
          window.localStorage.setItem('akahu_oauth_state', result.state);
        }
        if (typeof window !== 'undefined') {
          window.location.href = result.authorizationUrl;
        }
        return;
      }

      // Personal App mode — no OAuth redirect. Trigger the classic→official
      // migration directly; the user has already completed the upgrade on
      // Akahu's side (my.akahu.nz).
      const bankName = connection.bankName ?? t('row.unknownBank');
      const migration = await apiClient.migrateAkahuConnection(connection.connectionId);

      if (migration.success) {
        toast.success(
          t('toasts.migrationSuccess', {
            bankName,
            count: migration.transactionsRemapped,
          }),
        );
        // Refresh so the migrated connection drops off the banner.
        try {
          setStatus(await apiClient.getAkahuMigrationStatus());
        } catch {
          // Non-fatal — the row clears on the next page load.
        }
      } else {
        toast.error(
          t('toasts.migrationFailed', {
            bankName,
            error: migration.errorMessage ?? '',
          }),
        );
      }
    } catch (error) {
      console.error('Failed to initiate Akahu upgrade', error);
      toast.error(t('toasts.initiateFailed'));
    } finally {
      setUpgradingId(null);
      setIsInitiatingAny(false);
    }
  }, [t, isInitiatingAny]);

  if (!status || status.pendingConnections.length === 0 || dismissed) {
    return null;
  }

  const now = new Date();
  const deadline = new Date(status.deadline);
  const daysRemainingRaw = Number.isNaN(deadline.getTime())
    ? 0
    : differenceInCalendarDays(deadline, now);
  const daysRemaining = Math.max(0, daysRemainingRaw);

  return (
    <div
      role="alert"
      aria-label={t('ariaLabel')}
      data-testid="akahu-migration-banner"
      className={cn(
        'relative rounded-2xl border border-amber-300/70 bg-gradient-to-br from-amber-50 via-amber-50/60 to-orange-50/50 p-5 shadow-sm mb-6',
        className,
      )}
    >
      <button
        type="button"
        onClick={handleDismiss}
        aria-label={t('dismiss')}
        data-testid="akahu-migration-banner-dismiss"
        className="absolute right-3 top-3 rounded-lg p-1.5 text-amber-700 hover:bg-amber-100 hover:text-amber-900 focus:outline-none focus:ring-2 focus:ring-amber-500"
      >
        <XMarkIcon className="h-4 w-4" />
      </button>

      <div className="flex items-start gap-4 pr-8">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-amber-100 ring-1 ring-amber-200">
          <ExclamationTriangleIcon className="h-5 w-5 text-amber-700" />
        </div>

        <div className="min-w-0 flex-1">
          <h3
            data-testid="akahu-migration-banner-heading"
            className="font-[var(--font-dash-sans)] text-base font-semibold text-amber-900"
          >
            {t('heading')}
          </h3>
          <p className="mt-1 text-sm text-amber-800">
            {t('body', { daysRemaining })}
          </p>

          <ul
            className="mt-4 space-y-2"
            data-testid="akahu-migration-banner-list"
          >
            {status.pendingConnections.map((connection) => {
              const daysSinceSync = lastSyncedDays(connection, now);
              const bankLabel =
                connection.bankName ?? t('row.unknownBank');
              const lastSyncedLabel =
                daysSinceSync === null
                  ? t('row.neverSynced')
                  : t('row.lastSynced', { days: daysSinceSync });

              return (
                <li
                  key={connection.connectionId}
                  data-testid="akahu-migration-banner-row"
                  className="flex flex-col gap-2 rounded-xl border border-amber-200/70 bg-white/70 p-3 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="min-w-0">
                    <p className="text-sm font-medium text-ink-900 truncate">
                      {bankLabel}
                    </p>
                    <p className="text-xs text-ink-500 truncate">
                      {lastSyncedLabel}
                    </p>
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    onClick={() => handleUpgrade(connection)}
                    disabled={isInitiatingAny}
                    data-testid="akahu-migration-banner-upgrade"
                    className="flex items-center gap-1.5"
                  >
                    <ArrowTopRightOnSquareIcon className="h-4 w-4" />
                    {upgradingId === connection.connectionId
                      ? t('upgrading')
                      : t('upgrade')}
                  </Button>
                </li>
              );
            })}
          </ul>
        </div>
      </div>
    </div>
  );
}
