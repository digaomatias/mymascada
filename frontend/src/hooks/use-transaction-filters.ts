'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';

export type DateFilter = 'all' | 'last7' | 'thisWeek' | 'thisMonth' | 'last30' | 'last3Months' | 'custom';
export type TransferFilter = 'all' | 'only' | 'exclude';
export type ReviewFilter = 'all' | 'reviewed' | 'not-reviewed';
export type TypeFilter = 'all' | 'income' | 'expense';
export type ReconciliationFilter = 'all' | 'reconciled' | 'not-reconciled';
export type SortField = 'transactionDate' | 'amount' | 'description' | 'category';
export type SortDirection = 'asc' | 'desc';

export const useTransactionFilters = () => {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const [searchTerm, setSearchTerm] = useState(searchParams.get('search') || '');
  const [currentPage, setCurrentPage] = useState(parseInt(searchParams.get('page') || '1', 10));
  const [transferFilter, setTransferFilter] = useState<TransferFilter>((searchParams.get('transfers') as TransferFilter) || 'all');
  const [reviewFilter, setReviewFilter] = useState<ReviewFilter>((searchParams.get('review') as ReviewFilter) || 'all');
  const [typeFilter, setTypeFilter] = useState<TypeFilter>((searchParams.get('type') as TypeFilter) || 'all');
  const [reconciliationFilter, setReconciliationFilter] = useState<ReconciliationFilter>((searchParams.get('reconciliation') as ReconciliationFilter) || 'all');
  const [selectedCategoryId, setSelectedCategoryId] = useState(searchParams.get('categoryId') || '');
  const [selectedAccountId, setSelectedAccountId] = useState(searchParams.get('accountId') || '');
  const [dateFilter, setDateFilter] = useState<DateFilter>((searchParams.get('dateFilter') as DateFilter) || 'all');
  const [startDate, setStartDate] = useState(searchParams.get('startDate') || '');
  const [endDate, setEndDate] = useState(searchParams.get('endDate') || '');
  const [isSelectionMode, setIsSelectionMode] = useState(false);
  const [selectedTransactionIds, setSelectedTransactionIds] = useState<Set<number>>(new Set());
  const [bulkCategorizing, setBulkCategorizing] = useState(false);
  const [showBulkDeleteDialog, setShowBulkDeleteDialog] = useState(false);
  const [allCategories, setAllCategories] = useState<Array<{ id: number; name: string; fullPath?: string; type: number; parentId: number | null }>>([]);
  const [sortBy, setSortBy] = useState<SortField>((searchParams.get('sortBy') as SortField) || 'transactionDate');
  const [sortDirection, setSortDirection] = useState<SortDirection>((searchParams.get('sortDir') as SortDirection) || 'desc');

  const createQueryString = useCallback(() => {
    const params = new URLSearchParams();
    if (searchTerm) params.set('search', searchTerm);
    if (currentPage > 1) params.set('page', currentPage.toString());
    if (transferFilter !== 'all') params.set('transfers', transferFilter);
    if (reviewFilter !== 'all') params.set('review', reviewFilter);
    if (typeFilter !== 'all') params.set('type', typeFilter);
    if (reconciliationFilter !== 'all') params.set('reconciliation', reconciliationFilter);
    if (selectedCategoryId) params.set('categoryId', selectedCategoryId);
    if (selectedAccountId) params.set('accountId', selectedAccountId);
    if (dateFilter !== 'all') params.set('dateFilter', dateFilter);
    if (dateFilter === 'custom' && startDate) params.set('startDate', startDate);
    if (dateFilter === 'custom' && endDate) params.set('endDate', endDate);
    // Only add sort params if non-default (to keep URLs clean)
    if (sortBy !== 'transactionDate') params.set('sortBy', sortBy);
    if (sortDirection !== 'desc') params.set('sortDir', sortDirection);
    return params.toString();
  }, [
    searchTerm,
    currentPage,
    transferFilter,
    reviewFilter,
    typeFilter,
    reconciliationFilter,
    selectedCategoryId,
    selectedAccountId,
    dateFilter,
    startDate,
    endDate,
    sortBy,
    sortDirection,
  ]);

  useEffect(() => {
    const queryString = createQueryString();
    router.push(`${pathname}?${queryString}`);
  }, [createQueryString, pathname, router]);

  // Reset page to 1 when account filter changes
  useEffect(() => {
    if (selectedAccountId) {
      setCurrentPage(1);
    }
  }, [selectedAccountId]);

  // Reset page to 1 when sort changes
  useEffect(() => {
    setCurrentPage(1);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sortBy, sortDirection]);

  const handleSetCurrentPage = (page: number) => {
    setCurrentPage(page);
  }

  return {
    searchTerm,
    setSearchTerm,
    currentPage,
    setCurrentPage: handleSetCurrentPage,
    transferFilter,
    setTransferFilter,
    reviewFilter,
    setReviewFilter,
    typeFilter,
    setTypeFilter,
    reconciliationFilter,
    setReconciliationFilter,
    selectedCategoryId,
    setSelectedCategoryId,
    selectedAccountId,
    setSelectedAccountId,
    dateFilter,
    setDateFilter,
    startDate,
    setStartDate,
    endDate,
    setEndDate,
    isSelectionMode,
    setIsSelectionMode,
    selectedTransactionIds,
    setSelectedTransactionIds,
    bulkCategorizing,
    setBulkCategorizing,
    showBulkDeleteDialog,
    setShowBulkDeleteDialog,
    allCategories,
    setAllCategories,
    sortBy,
    setSortBy,
    sortDirection,
    setSortDirection,
  };
};
