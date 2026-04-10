'use client';

import { useState } from 'react';
import Link from 'next/link';
import { useTranslations } from 'next-intl';
import { useAuth } from '@/contexts/auth-context';
import { SparklesIcon, XMarkIcon } from '@heroicons/react/24/outline';
import { buttonVariants } from '@/components/ui/button';
import { cn } from '@/lib/utils';

type UpsellContext = 'uncategorizedBacklog' | 'ruleSuggestions' | 'postImport';

interface CategorizationUpsellBannerProps {
  context: UpsellContext;
  /**
   * Only used when context is 'uncategorizedBacklog' — the current count
   * of uncategorized transactions. Drives the banner description.
   */
  count?: number;
  /** Destination for the CTA. Defaults to the billing page. */
  upgradeHref?: string;
  className?: string;
  /** When true, the banner becomes dismissible with a close button. */
  dismissible?: boolean;
}

/**
 * Contextual upsell banner for free-tier users. Shown in three places:
 * - Uncategorized backlog on the dashboard
 * - Rule suggestions page (to unlock AI-enhanced suggestions)
 * - Post-import (to convert users who just finished a bulk import)
 *
 * Hidden for Pro/Family users (who already have the feature) AND for
 * self-hosted users (who have unlimited access via BYOK). Never shown
 * when the user hasn't loaded yet.
 */
export function CategorizationUpsellBanner({
  context,
  count,
  // Billing lives at /settings/billing (confirmed by the existing
  // frontend/src/app/settings/billing/ route) — the older
  // `/settings?tab=billing` form doesn't resolve to the purchase flow.
  upgradeHref = '/settings/billing',
  className,
  dismissible = false,
}: CategorizationUpsellBannerProps) {
  const { user } = useAuth();
  const t = useTranslations('upsell.categorization');
  const [dismissed, setDismissed] = useState(false);

  // Hide for non-free tiers and for self-hosted deployments
  if (!user) return null;
  if (user.isSelfHosted) return null;
  if (user.subscriptionTier && user.subscriptionTier !== 'Free') return null;
  if (dismissed) return null;

  const sectionKey = context;

  return (
    <div
      className={cn(
        'relative flex items-start gap-4 rounded-2xl border border-primary-200/60 bg-gradient-to-br from-primary-50/80 via-white to-amber-50/60 p-5 shadow-sm',
        className,
      )}
      data-testid="categorization-upsell-banner"
    >
      <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-primary-500 to-primary-400 shadow-lg">
        <SparklesIcon className="h-5 w-5 text-white" />
      </div>
      <div className="min-w-0 flex-1">
        <h3 className="font-[var(--font-dash-sans)] text-base font-semibold text-ink-900">
          {t(`${sectionKey}.title`)}
        </h3>
        <p className="mt-1 text-sm text-ink-600">
          {context === 'uncategorizedBacklog'
            ? t(`${sectionKey}.description`, { count: count ?? 0 })
            : t(`${sectionKey}.description`)}
        </p>
        <div className="mt-3">
          {/*
            Render the CTA as a styled anchor rather than wrapping a <Button>
            inside a <Link>. Wrapping would emit <a><button></button></a> which
            nests interactive elements, is invalid HTML, and breaks
            accessibility tooling. We reuse `buttonVariants` so the styling
            stays in sync with the rest of the design system.
          */}
          <Link
            href={upgradeHref}
            className={cn(
              buttonVariants({ variant: 'primary', size: 'sm' }),
              'bg-primary-600 hover:bg-primary-700 text-white inline-flex items-center',
            )}
          >
            {t(`${sectionKey}.cta`)}
          </Link>
        </div>
      </div>
      {dismissible && (
        <button
          type="button"
          onClick={() => setDismissed(true)}
          className="shrink-0 rounded-lg p-1 text-ink-400 hover:bg-ink-100 hover:text-ink-600"
          aria-label={t('dismiss')}
        >
          <XMarkIcon className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}
