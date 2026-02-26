'use client';

import React, { useState, useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { formatCurrency, cn } from '@/lib/utils';
import { CategoryTrendData } from '@/lib/api-client';
import { MagnifyingGlassIcon, XMarkIcon } from '@heroicons/react/24/outline';
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
    <div className="rounded-[26px] border border-violet-100/60 bg-white/90 p-4 shadow-lg shadow-violet-200/20 backdrop-blur-xs">
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-[var(--font-dash-sans)] font-semibold text-slate-900">{t('selectCategories')}</h3>
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
          className="w-full pl-9 pr-8 py-2 text-sm border border-slate-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-violet-500 focus:border-transparent"
        />
        {searchTerm && (
          <button
            onClick={() => setSearchTerm('')}
            className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
          >
            <XMarkIcon className="w-4 h-4" />
          </button>
        )}
      </div>

      {/* Quick selection buttons */}
      <div className="flex flex-wrap gap-2 mb-3">
        <Button
          variant="secondary"
          size="sm"
          onClick={() => selectTopN(5)}
          className="text-xs"
        >
          {t('top5')}
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={selectAll}
          className="text-xs"
        >
          {t('selectAll')}
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={clearSelection}
          className="text-xs"
        >
          {t('clear')}
        </Button>
      </div>

      {/* Warning when at max */}
      {selectedCategoryIds.length >= maxSelections && (
        <div className="mb-3 p-2 bg-yellow-50 border border-yellow-200 rounded-lg text-sm text-yellow-800">
          {t('maxCategoriesWarning', { max: maxSelections })}
        </div>
      )}

      {/* Category list */}
      <div className="space-y-1 max-h-[300px] overflow-y-auto">
        {displayCategories.map((category) => {
          const isSelected = selectedCategoryIds.includes(category.categoryId);
          const isDisabled = !isSelected && selectedCategoryIds.length >= maxSelections;

          return (
            <button
              key={category.categoryId}
              onClick={() => !isDisabled && toggleCategory(category.categoryId)}
              disabled={isDisabled}
              className={cn(
                'w-full flex items-center justify-between p-2 rounded-lg transition-colors',
                isSelected && 'bg-violet-50 border border-violet-200',
                isDisabled && 'bg-slate-50 opacity-50 cursor-not-allowed',
                !isSelected && !isDisabled && 'hover:bg-slate-50 border border-transparent'
              )}
            >
              <div className="flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={isSelected}
                  onChange={() => {}}
                  disabled={isDisabled}
                  className="h-4 w-4 rounded border-slate-300 text-violet-600 focus:ring-violet-500"
                />
                <div
                  className="w-3 h-3 rounded-full"
                  style={{ backgroundColor: category.categoryColor || '#8B5CF6' }}
                />
                <span className="text-sm font-medium text-slate-900 truncate max-w-[150px]">
                  {category.categoryName}
                </span>
              </div>
              <span className="font-[var(--font-dash-mono)] text-sm text-slate-500">
                {formatCurrency(category.totalSpent)}
              </span>
            </button>
          );
        })}
      </div>

      {/* Show more/less button */}
      {hasMore && !searchTerm && (
        <button
          onClick={() => setIsExpanded(!isExpanded)}
          className="mt-2 text-sm text-violet-600 hover:text-violet-700 font-medium"
        >
          {isExpanded ? t('showLess') : t('showMore', { count: sortedCategories.length - 8 })}
        </button>
      )}

      {filteredCategories.length === 0 && (
        <p className="text-center text-slate-500 text-sm py-4">
          {t('noMatchingCategories')}
        </p>
      )}
    </div>
  );
}
