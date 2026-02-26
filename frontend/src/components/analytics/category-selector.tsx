'use client';

import React, { useState, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { formatCurrency, cn } from '@/lib/utils';
import { CategoryTrendData } from '@/lib/api-client';
import { MagnifyingGlassIcon, XMarkIcon } from '@heroicons/react/24/outline';
import { CheckIcon } from '@heroicons/react/24/solid';
import { useTranslations } from 'next-intl';

interface CategorySelectorProps {
  categories: CategoryTrendData[];
  selectedCategoryIds: number[];
  onSelectionChange: (categoryIds: number[]) => void;
  maxSelections?: number;
}

export function CategorySelector({
  categories,
  selectedCategoryIds,
  onSelectionChange,
  maxSelections = 10,
}: CategorySelectorProps) {
  const t = useTranslations('analytics.categoryTrends');
  const [searchTerm, setSearchTerm] = useState('');
  const [isExpanded, setIsExpanded] = useState(false);

  // Filter categories by search term
  const filteredCategories = useMemo(() => {
    if (!searchTerm.trim()) {
      return categories;
    }
    const term = searchTerm.toLowerCase();
    return categories.filter((cat) =>
      cat.categoryName.toLowerCase().includes(term)
    );
  }, [categories, searchTerm]);

  // Sort by total spent (descending)
  const sortedCategories = useMemo(() => {
    return [...filteredCategories].sort((a, b) => b.totalSpent - a.totalSpent);
  }, [filteredCategories]);

  const toggleCategory = (categoryId: number) => {
    if (selectedCategoryIds.includes(categoryId)) {
      onSelectionChange(selectedCategoryIds.filter((id) => id !== categoryId));
    } else {
      if (selectedCategoryIds.length >= maxSelections) {
        // Show warning or don't add
        return;
      }
      onSelectionChange([...selectedCategoryIds, categoryId]);
    }
  };

  const selectTopN = (n: number) => {
    const topIds = sortedCategories.slice(0, n).map((cat) => cat.categoryId);
    onSelectionChange(topIds);
  };

  const clearSelection = () => {
    onSelectionChange([]);
  };

  const selectAll = () => {
    const allIds = sortedCategories.slice(0, maxSelections).map((cat) => cat.categoryId);
    onSelectionChange(allIds);
  };

  const displayCategories = isExpanded ? sortedCategories : sortedCategories.slice(0, 8);
  const hasMore = sortedCategories.length > 8;

  return (
    <aside className="rounded-[26px] border border-violet-100/70 bg-white/92 p-4 shadow-[0_20px_44px_-32px_rgba(76,29,149,0.42)] backdrop-blur-xs">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="font-[var(--font-dash-sans)] text-base font-semibold tracking-[-0.01em] text-slate-900">
          {t('selectCategories')}
        </h3>
        <span className="text-xs text-slate-500">
          {t('nOfMaxSelected', { count: selectedCategoryIds.length, max: maxSelections })}
        </span>
      </div>

      {/* Search input */}
      <div className="relative mb-3">
        <MagnifyingGlassIcon className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
        <input
          type="text"
          placeholder={t('searchCategories')}
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          className="w-full rounded-xl border border-violet-100/80 bg-white pl-9 pr-8 py-2 text-sm text-slate-700 transition-colors placeholder:text-slate-400 focus:outline-hidden focus:ring-2 focus:ring-violet-200 focus:border-violet-300"
        />
        {searchTerm && (
          <button
            type="button"
            onClick={() => setSearchTerm('')}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
          >
            <XMarkIcon className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Quick selection buttons */}
      <div className="mb-3 flex flex-wrap gap-2">
        <Button
          variant="secondary"
          size="sm"
          onClick={() => selectTopN(5)}
          className="h-8 rounded-lg px-3 py-1.5 text-[11px] uppercase tracking-[0.08em]"
        >
          {t('top5')}
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={selectAll}
          className="h-8 rounded-lg px-3 py-1.5 text-[11px] uppercase tracking-[0.08em]"
        >
          {t('selectAll')}
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={clearSelection}
          className="h-8 rounded-lg px-3 py-1.5 text-[11px] uppercase tracking-[0.08em]"
        >
          {t('clear')}
        </Button>
      </div>

      {/* Warning when at max */}
      {selectedCategoryIds.length >= maxSelections && (
        <div className="mb-3 rounded-xl border border-amber-200 bg-amber-50/70 p-2.5 text-xs font-medium text-amber-800">
          {t('maxCategoriesWarning', { max: maxSelections })}
        </div>
      )}

      {/* Category list */}
      <div className="max-h-[330px] space-y-1 overflow-y-auto pr-1">
        {displayCategories.map((category) => {
          const isSelected = selectedCategoryIds.includes(category.categoryId);
          const isDisabled = !isSelected && selectedCategoryIds.length >= maxSelections;

          return (
            <button
              key={category.categoryId}
              type="button"
              onClick={() => !isDisabled && toggleCategory(category.categoryId)}
              disabled={isDisabled}
              className={cn(
                'w-full rounded-xl border p-2.5 text-left transition-all',
                isSelected &&
                  'border-violet-200 bg-violet-50/75 shadow-[0_12px_26px_-20px_rgba(124,58,237,0.65)]',
                isDisabled && 'border-slate-100 bg-slate-50 opacity-55 cursor-not-allowed',
                !isSelected && !isDisabled && 'border-transparent hover:border-violet-100 hover:bg-violet-50/40'
              )}
            >
              <div className="flex items-center justify-between gap-2">
                <div className="flex min-w-0 items-center gap-2.5">
                  <span
                    className={cn(
                      'flex h-4 w-4 items-center justify-center rounded border transition-colors',
                      isSelected ? 'border-violet-500 bg-violet-500 text-white' : 'border-slate-300 bg-white',
                    )}
                  >
                    {isSelected && <CheckIcon className="h-3 w-3" />}
                  </span>
                  <div
                    className="h-3 w-3 shrink-0 rounded-full"
                    style={{ backgroundColor: category.categoryColor || '#8B5CF6' }}
                  />
                  <span className="truncate text-sm font-medium text-slate-800">
                    {category.categoryName}
                  </span>
                </div>
                <div
                  className={cn(
                    'font-[var(--font-dash-mono)] text-sm',
                    isSelected ? 'text-violet-700' : 'text-slate-500',
                  )}
                >
                  {formatCurrency(category.totalSpent)}
                </div>
              </div>
            </button>
          );
        })}
      </div>

      {/* Show more/less button */}
      {hasMore && !searchTerm && (
        <button
          type="button"
          onClick={() => setIsExpanded(!isExpanded)}
          className="mt-2 text-sm font-medium text-violet-600 transition-colors hover:text-violet-700"
        >
          {isExpanded ? t('showLess') : t('showMore', { count: sortedCategories.length - 8 })}
        </button>
      )}

      {filteredCategories.length === 0 && (
        <p className="py-4 text-center text-sm text-slate-500">
          {t('noMatchingCategories')}
        </p>
      )}
    </aside>
  );
}
