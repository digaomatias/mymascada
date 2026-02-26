'use client';

import React from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import { ChevronLeftIcon, ChevronRightIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

export type PeriodType = 'month' | 'quarter' | 'year' | 'all';

interface PeriodSelectorProps {
  period: PeriodType;
  selectedYear: number;
  selectedMonth: number;
  onPeriodChange: (period: PeriodType) => void;
  onYearChange: (year: number) => void;
  onMonthChange: (month: number) => void;
}

export function PeriodSelector({
  period,
  selectedYear,
  selectedMonth,
  onPeriodChange,
  onYearChange,
  onMonthChange,
}: PeriodSelectorProps) {
  const t = useTranslations('analytics');
  const now = new Date();
  const currentYear = now.getFullYear();
  const currentMonth = now.getMonth() + 1;
  const periodOptions: PeriodType[] = ['month', 'quarter', 'year', 'all'];

  // Get the current quarter (1-4)
  const getQuarter = (month: number) => Math.ceil(month / 3);
  const selectedQuarter = getQuarter(selectedMonth);
  const currentQuarter = getQuarter(currentMonth);

  // Format the period label
  const getPeriodLabel = (): string => {
    switch (period) {
      case 'month':
        return new Date(selectedYear, selectedMonth - 1).toLocaleDateString('en-US', {
          month: 'long',
          year: 'numeric',
        });
      case 'quarter':
        return `Q${selectedQuarter} ${selectedYear}`;
      case 'year':
        return `${selectedYear}`;
      case 'all':
        return t('periods.allTime');
    }
  };

  // Check if we can navigate forward
  const canNavigateForward = (): boolean => {
    switch (period) {
      case 'month':
        return selectedYear < currentYear || (selectedYear === currentYear && selectedMonth < currentMonth);
      case 'quarter':
        return selectedYear < currentYear || (selectedYear === currentYear && selectedQuarter < currentQuarter);
      case 'year':
        return selectedYear < currentYear;
      case 'all':
        return false;
    }
  };

  // Navigate to previous period
  const navigatePrevious = () => {
    switch (period) {
      case 'month':
        if (selectedMonth === 1) {
          onYearChange(selectedYear - 1);
          onMonthChange(12);
        } else {
          onMonthChange(selectedMonth - 1);
        }
        break;
      case 'quarter':
        if (selectedQuarter === 1) {
          onYearChange(selectedYear - 1);
          onMonthChange(10); // October (Q4 start)
        } else {
          onMonthChange((selectedQuarter - 2) * 3 + 1); // First month of previous quarter
        }
        break;
      case 'year':
        onYearChange(selectedYear - 1);
        break;
    }
  };

  // Navigate to next period
  const navigateNext = () => {
    if (!canNavigateForward()) return;

    switch (period) {
      case 'month':
        if (selectedMonth === 12) {
          onYearChange(selectedYear + 1);
          onMonthChange(1);
        } else {
          onMonthChange(selectedMonth + 1);
        }
        break;
      case 'quarter':
        if (selectedQuarter === 4) {
          onYearChange(selectedYear + 1);
          onMonthChange(1); // January (Q1 start)
        } else {
          onMonthChange(selectedQuarter * 3 + 1); // First month of next quarter
        }
        break;
      case 'year':
        onYearChange(selectedYear + 1);
        break;
    }
  };

  // Show navigation only for month, quarter, and year
  const showNavigation = period !== 'all';

  return (
    <div className="mb-6 rounded-[24px] border border-violet-100/80 bg-white/92 p-4 shadow-[0_18px_40px_-30px_rgba(76,29,149,0.45)] backdrop-blur-xs">
      <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div className="inline-flex w-full flex-wrap items-center gap-1 rounded-xl border border-violet-100 bg-violet-50/55 p-1 lg:w-auto">
          {periodOptions.map((option) => {
            const isActive = option === period;
            const label =
              option === 'month'
                ? t('periods.month')
                : option === 'quarter'
                  ? t('periods.quarter')
                  : option === 'year'
                    ? t('periods.year')
                    : t('periods.allTime');

            return (
              <button
                key={option}
                type="button"
                onClick={() => onPeriodChange(option)}
                className={cn(
                  'flex-1 rounded-lg px-3 py-2 text-xs font-semibold uppercase tracking-[0.08em] transition-all lg:flex-none',
                  isActive
                    ? 'bg-white text-violet-700 shadow-[0_10px_24px_-16px_rgba(76,29,149,0.65)]'
                    : 'text-slate-500 hover:text-violet-700',
                )}
                aria-pressed={isActive}
              >
                {label}
              </button>
            );
          })}
        </div>

        {showNavigation ? (
          <div className="flex w-full items-center justify-between gap-2 rounded-xl border border-violet-100/80 bg-white/95 px-2 py-1.5 lg:w-auto lg:justify-start">
            <Button
              variant="ghost"
              size="icon"
              onClick={navigatePrevious}
              className="h-8 w-8 rounded-lg text-slate-500 hover:text-violet-700"
              aria-label={t('previousPeriod')}
            >
              <ChevronLeftIcon className="h-4 w-4" />
            </Button>

            <span className="min-w-[150px] text-center font-[var(--font-dash-sans)] text-sm font-semibold tracking-[-0.01em] text-slate-900">
              {getPeriodLabel()}
            </span>

            <Button
              variant="ghost"
              size="icon"
              onClick={navigateNext}
              disabled={!canNavigateForward()}
              className="h-8 w-8 rounded-lg text-slate-500 hover:text-violet-700"
              aria-label={t('nextPeriod')}
            >
              <ChevronRightIcon className="h-4 w-4" />
            </Button>
          </div>
        ) : (
          <div className="inline-flex items-center rounded-xl border border-violet-100/80 bg-white/95 px-3 py-2 text-sm font-semibold text-slate-700">
            {getPeriodLabel()}
          </div>
        )}
      </div>
    </div>
  );
}
