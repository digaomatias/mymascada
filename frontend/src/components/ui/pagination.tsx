'use client';

import React from 'react';
import { Button } from '@/components/ui/button';
import { ChevronLeftIcon, ChevronRightIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

export interface PaginationInfo {
  currentPage: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface PaginationProps {
  pagination: PaginationInfo;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  pageSizeOptions?: number[];
  loading?: boolean;
  className?: string;
}

const DEFAULT_PAGE_SIZES = [10, 25, 50];

export function Pagination({
  pagination,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = DEFAULT_PAGE_SIZES,
  loading = false,
  className = ''
}: PaginationProps) {
  const tCommon = useTranslations('common');
  const tPagination = useTranslations('pagination');
  const { currentPage, totalPages, totalCount, pageSize, hasNextPage, hasPreviousPage } = pagination;

  // Calculate display range
  const startItem = totalCount === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endItem = Math.min(currentPage * pageSize, totalCount);

  if (totalCount === 0) {
    return null;
  }

  return (
    <div className={`flex items-center justify-between border-t border-gray-100 bg-white px-4 py-3 sm:px-6 ${className}`}>
      {/* Results info */}
      <div className="flex flex-1 justify-between sm:hidden">
        <span className="text-sm text-gray-700">
          {tPagination('rangeOfTotal', {
            start: startItem,
            end: endItem,
            total: totalCount.toLocaleString()
          })}
        </span>
        <span className="text-sm text-gray-700">
          {tPagination('pageOfTotal', { current: currentPage, total: totalPages })}
        </span>
      </div>

      <div className="hidden sm:flex sm:flex-1 sm:items-center sm:justify-between">
        {/* Desktop: Results info and page size selector */}
        <div className="flex items-center gap-4">
          <p className="text-sm text-gray-700">
            {tPagination('showingResults', {
              start: startItem.toLocaleString(),
              end: endItem.toLocaleString(),
              total: totalCount.toLocaleString()
            })}
          </p>
          
          {/* Page size selector */}
          <div className="flex items-center gap-2">
            <label htmlFor="pageSize" className="text-sm text-gray-700">
              {tPagination('show')}
            </label>
            <select
              id="pageSize"
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              disabled={loading}
              className="rounded-md border border-gray-300 bg-white px-2 py-1 text-sm focus:border-purple-500 focus:outline-none focus:ring-1 focus:ring-purple-500 disabled:opacity-50"
            >
              {pageSizeOptions.map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
            <span className="text-sm text-gray-700">{tPagination('perPage')}</span>
          </div>
        </div>

        {/* Desktop: Pagination controls */}
        <div className="flex items-center gap-2">
          <Button
            variant="secondary"
            size="sm"
            onClick={() => onPageChange(currentPage - 1)}
            disabled={!hasPreviousPage || loading}
          >
            <ChevronLeftIcon className="w-4 h-4 mr-1" />
            {tCommon('previous')}
          </Button>
          
          {/* Page numbers */}
          <div className="flex items-center gap-1">
            {renderPageNumbers(currentPage, totalPages, onPageChange, loading)}
          </div>
          
          <Button
            variant="secondary"
            size="sm"
            onClick={() => onPageChange(currentPage + 1)}
            disabled={!hasNextPage || loading}
          >
            {tCommon('next')}
            <ChevronRightIcon className="w-4 h-4 ml-1" />
          </Button>
        </div>
      </div>

      {/* Mobile: Navigation controls */}
      <div className="flex flex-1 justify-between sm:hidden">
        <Button
          variant="secondary"
          size="sm"
          onClick={() => onPageChange(currentPage - 1)}
          disabled={!hasPreviousPage || loading}
          className="flex items-center"
        >
          <ChevronLeftIcon className="w-4 h-4 mr-1" />
          {tCommon('previous')}
        </Button>
        
        <Button
          variant="secondary"
          size="sm"
          onClick={() => onPageChange(currentPage + 1)}
          disabled={!hasNextPage || loading}
          className="flex items-center"
        >
          {tCommon('next')}
          <ChevronRightIcon className="w-4 h-4 ml-1" />
        </Button>
      </div>
    </div>
  );
}

function renderPageNumbers(
  currentPage: number,
  totalPages: number,
  onPageChange: (page: number) => void,
  loading: boolean
) {
  const pages: (number | string)[] = [];
  const showEllipsis = totalPages > 7;

  if (!showEllipsis) {
    // Show all pages if 7 or fewer
    for (let i = 1; i <= totalPages; i++) {
      pages.push(i);
    }
  } else {
    // Show ellipsis pattern for many pages
    if (currentPage <= 4) {
      // Near beginning: 1 2 3 4 5 ... 20
      pages.push(1, 2, 3, 4, 5, '...', totalPages);
    } else if (currentPage >= totalPages - 3) {
      // Near end: 1 ... 16 17 18 19 20
      pages.push(1, '...', totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages);
    } else {
      // Middle: 1 ... 8 9 10 ... 20
      pages.push(1, '...', currentPage - 1, currentPage, currentPage + 1, '...', totalPages);
    }
  }

  return pages.map((page, index) => {
    if (page === '...') {
      return (
        <span key={`ellipsis-${index}`} className="px-2 py-1 text-sm text-gray-500">
          ...
        </span>
      );
    }

    const pageNum = page as number;
    const isCurrent = pageNum === currentPage;

    return (
      <button
        key={pageNum}
        onClick={() => onPageChange(pageNum)}
        disabled={loading}
        className={`
          px-3 py-1 text-sm font-medium rounded-md transition-colors
          ${isCurrent
            ? 'bg-purple-600 text-white'
            : 'text-gray-700 hover:bg-gray-100 focus:bg-gray-100'
          }
          disabled:opacity-50 disabled:cursor-not-allowed
        `}
      >
        {pageNum}
      </button>
    );
  });
}

// Hook for managing pagination state
export function usePagination(initialPageSize: number = 25) {
  const [currentPage, setCurrentPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(initialPageSize);
  const [totalCount, setTotalCount] = React.useState(0);
  const [totalPages, setTotalPages] = React.useState(1);

  const handlePageChange = React.useCallback((page: number) => {
    setCurrentPage(page);
  }, []);

  const handlePageSizeChange = React.useCallback((newPageSize: number) => {
    setPageSize(newPageSize);
    setCurrentPage(1); // Reset to first page when changing page size
  }, []);

  const updatePagination = React.useCallback((response: { totalCount: number; totalPages: number; page: number }) => {
    setTotalCount(response.totalCount);
    setTotalPages(response.totalPages);
    setCurrentPage(response.page);
  }, []);

  const paginationInfo: PaginationInfo = {
    currentPage,
    totalPages,
    totalCount,
    pageSize,
    hasNextPage: currentPage < totalPages,
    hasPreviousPage: currentPage > 1
  };

  return {
    paginationInfo,
    handlePageChange,
    handlePageSizeChange,
    updatePagination,
    currentPage,
    pageSize
  };
}
