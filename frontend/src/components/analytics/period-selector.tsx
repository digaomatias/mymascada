'use client';

import React from 'react';
import { Button } from '@/components/ui/button';
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
        return 'All Time';
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
    <div className="mb-6 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 rounded-[26px] border border-violet-100/60 bg-white/90 p-4 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
      {/* Period Type Selector */}
      <div className="flex gap-2">
        <Button
          variant={period === 'month' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => onPeriodChange('month')}
        >
          Month
        </Button>
        <Button
          variant={period === 'quarter' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => onPeriodChange('quarter')}
        >
          Quarter
        </Button>
        <Button
          variant={period === 'year' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => onPeriodChange('year')}
        >
          Year
        </Button>
        <Button
          variant={period === 'all' ? 'primary' : 'secondary'}
          size="sm"
          onClick={() => onPeriodChange('all')}
        >
          All Time
        </Button>
      </div>

      {/* Period Navigation */}
      {showNavigation && (
        <div className="flex items-center gap-2">
          <Button
            variant="ghost"
            size="sm"
            onClick={navigatePrevious}
            className="p-2"
            aria-label={t('previousPeriod')}
          >
            <ChevronLeftIcon className="w-5 h-5" />
          </Button>

          <span className="min-w-[140px] text-center font-[var(--font-dash-sans)] font-semibold text-slate-900">
            {getPeriodLabel()}
          </span>

          <Button
            variant="ghost"
            size="sm"
            onClick={navigateNext}
            disabled={!canNavigateForward()}
            className="p-2"
            aria-label={t('nextPeriod')}
          >
            <ChevronRightIcon className="w-5 h-5" />
          </Button>
        </div>
      )}

      {/* Show static label for All Time */}
      {!showNavigation && (
        <span className="font-[var(--font-dash-sans)] font-semibold text-slate-900">{getPeriodLabel()}</span>
      )}
    </div>
  );
}
