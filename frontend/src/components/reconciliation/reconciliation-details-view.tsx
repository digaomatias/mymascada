'use client';

import { useState, useEffect, useCallback } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { ConfirmationDialog } from '@/components/ui/confirmation-dialog';
import { TransactionComparison } from './transaction-comparison';
import { ManualMatchingModal } from './manual-matching-modal';
import { DraggableTransactionCard } from './draggable-transaction-card';
import { ReconciliationBalanceCards } from './reconciliation-balance-cards';
import { CreateTransactionModal } from '../modals/create-transaction-modal';
import { useDragDrop, DragDropItem } from '@/hooks/use-drag-drop';
import {
  CheckCircleIcon,
  ExclamationTriangleIcon,
  XMarkIcon,
  MagnifyingGlassIcon,
  FunnelIcon,
  ArrowsUpDownIcon,
  LinkSlashIcon,
  ArrowDownTrayIcon
} from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { useFeatures } from '@/contexts/features-context';
import { useAuth } from '@/contexts/auth-context';
import { toast } from 'sonner';

interface BankTransaction {
  bankTransactionId: string;
  amount: number;
  transactionDate: string;
  description: string;
  bankCategory?: string;
}

interface SystemTransaction {
  id: number;
  amount: number;
  description: string;
  transactionDate: string;
  categoryName?: string;
  status: number;
}

interface ReconciliationItemDetail {
  id: number;
  reconciliationId: number;
  transactionId?: number;
  itemType: number; // ReconciliationItemType enum
  matchConfidence?: number;
  matchMethod?: number; // MatchMethod enum
  bankTransaction?: BankTransaction;
  systemTransaction?: SystemTransaction;
  createdAt: string;
  updatedAt: string;
  displayAmount: string;
  displayDate: string;
  displayDescription: string;
  matchTypeLabel: string;
  matchConfidenceLabel: string;
}

interface ReconciliationDetailsSummary {
  totalItems: number;
  exactMatches: number;
  fuzzyMatches: number;
  unmatchedBank: number;
  unmatchedSystem: number;
  matchPercentage: number;
}

interface ReconciliationDetails {
  reconciliationId: number;
  summary: ReconciliationDetailsSummary;
  exactMatches: ReconciliationItemDetail[];
  fuzzyMatches: ReconciliationItemDetail[];
  unmatchedBankTransactions: ReconciliationItemDetail[];
  unmatchedSystemTransactions: ReconciliationItemDetail[];
}

interface PotentialMatch {
  systemTransaction: SystemTransaction;
  confidence: number;
  matchReasons: {
    amountMatch: boolean;
    dateMatch: boolean;
    descriptionSimilar: boolean;
    amountDifference: number;
    dateDifferenceInDays: number;
  };
}

interface ReconciliationDetailsViewProps {
  reconciliationId: number;
  accountId: number;
  onCompleteReconciliation: () => void;
  onBack: () => void;
  loading?: boolean;
  statementEndBalance?: number;
}

type TabType = 'exact' | 'fuzzy' | 'unmatched-bank' | 'unmatched-system';

const TAB_CONFIG_BASE = {
  exact: {
    labelKey: 'exactMatches' as const,
    icon: CheckCircleIcon,
    color: 'text-green-600',
    bgColor: 'bg-green-50',
    borderColor: 'border-green-200'
  },
  fuzzy: {
    labelKey: 'fuzzyMatches' as const,
    icon: ExclamationTriangleIcon,
    color: 'text-yellow-600',
    bgColor: 'bg-yellow-50',
    borderColor: 'border-yellow-200'
  },
  'unmatched-bank': {
    labelKey: 'unmatchedBank' as const,
    icon: XMarkIcon,
    color: 'text-red-600',
    bgColor: 'bg-red-50',
    borderColor: 'border-red-200'
  },
  'unmatched-system': {
    labelKey: 'unmatchedSystem' as const,
    icon: XMarkIcon,
    color: 'text-blue-600',
    bgColor: 'bg-blue-50',
    borderColor: 'border-blue-200'
  }
};

export function ReconciliationDetailsView({ 
  reconciliationId, 
  accountId,
  onCompleteReconciliation, 
  onBack, 
  loading = false,
  statementEndBalance = 0
}: ReconciliationDetailsViewProps) {
  const t = useTranslations('reconciliation');
  const tCommon = useTranslations('common');
  const tTransactions = useTranslations('transactions');
  const tToasts = useTranslations('toasts');
  const { features } = useFeatures();
  const { user } = useAuth();

  const TAB_CONFIG = Object.fromEntries(
    Object.entries(TAB_CONFIG_BASE).map(([key, val]) => [
      key,
      { ...val, label: t(val.labelKey) }
    ])
  ) as Record<TabType, { label: string; labelKey: string; icon: typeof CheckCircleIcon; color: string; bgColor: string; borderColor: string }>;

  const [details, setDetails] = useState<ReconciliationDetails | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<TabType>('exact');
  const [searchTerm, setSearchTerm] = useState('');
  const [minAmount, setMinAmount] = useState('');
  const [maxAmount, setMaxAmount] = useState('');
  const [expandedItems, setExpandedItems] = useState<Set<number>>(new Set());
  const [showMatchingModal, setShowMatchingModal] = useState(false);
  const [selectedBankTransaction, setSelectedBankTransaction] = useState<BankTransaction | null>(null);
  const [potentialMatches, setPotentialMatches] = useState<PotentialMatch[]>([]);
  const [loadingMatches, setLoadingMatches] = useState(false);
  const [showCreateTransactionModal, setShowCreateTransactionModal] = useState(false);
  const [createTransactionBankData, setCreateTransactionBankData] = useState<BankTransaction | null>(null);
  const [selectedItems, setSelectedItems] = useState<Set<number>>(new Set());
  const [showBulkActions, setShowBulkActions] = useState(false);
  const [bulkActionLoading, setBulkActionLoading] = useState(false);
  const [approvedItems, setApprovedItems] = useState<Set<number>>(new Set());
  const [isApproving, setIsApproving] = useState<Set<number>>(new Set());
  const [deleteConfirm, setDeleteConfirm] = useState<{ show: boolean; transactionId?: number }>({ show: false });
  const [bulkDeleteConfirm, setBulkDeleteConfirm] = useState({ show: false });
  const [bulkImportConfirm, setBulkImportConfirm] = useState({ show: false });
  const [importingItems, setImportingItems] = useState<Set<number>>(new Set());
  const [importedItems, setImportedItems] = useState<Set<number>>(new Set());
  
  // AI description preview state
  const [previewedDescriptions, setPreviewedDescriptions] = useState<Map<string, string>>(new Map());
  const [previewingItems, setPreviewingItems] = useState<Set<string>>(new Set());
  const showPreviewButton = features.aiCategorization && !!user?.aiDescriptionCleaning;

  // Drag and drop state
  const dragDrop = useDragDrop();

  const loadReconciliationDetails = useCallback(async () => {
    try {
      setIsLoading(true);
      const params = new URLSearchParams();
      
      if (searchTerm.trim()) {
        params.append('searchTerm', searchTerm.trim());
      }
      if (minAmount) {
        params.append('minAmount', minAmount);
      }
      if (maxAmount) {
        params.append('maxAmount', maxAmount);
      }

      const queryString = params.toString();
      const url = `/api/reconciliation/${reconciliationId}/details${queryString ? `?${queryString}` : ''}`;
      
      const result = await apiClient.get(url);
      setDetails(result as ReconciliationDetails);
    } catch (error) {
      console.error('Failed to load reconciliation details:', error);
      toast.error(tToasts('reconciliationDetailsLoadFailed'));
    } finally {
      setIsLoading(false);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reconciliationId, searchTerm, minAmount, maxAmount]);

  useEffect(() => {
    loadReconciliationDetails();
  }, [loadReconciliationDetails]);

  const handlePreviewDescription = async (bankTransactionId: string, rawDescription: string) => {
    setPreviewingItems(prev => new Set(prev).add(bankTransactionId));
    try {
      const result = await apiClient.previewDescriptionCleaning([{ rawDescription }]);
      if (result.results && result.results.length > 0) {
        const cleaned = result.results[0].cleanedDescription;
        setPreviewedDescriptions(prev => new Map(prev).set(bankTransactionId, cleaned));
      }
    } catch (error) {
      console.error('Failed to preview description cleaning:', error);
      toast.error(tToasts('reconciliationPreviewFailed'));
    } finally {
      setPreviewingItems(prev => {
        const next = new Set(prev);
        next.delete(bankTransactionId);
        return next;
      });
    }
  };

  const toggleExpanded = (itemId: number) => {
    setExpandedItems(prev => {
      const newSet = new Set(prev);
      if (newSet.has(itemId)) {
        newSet.delete(itemId);
      } else {
        newSet.add(itemId);
      }
      return newSet;
    });
  };

  const getTabData = (tab: TabType): ReconciliationItemDetail[] => {
    if (!details) return [];
    
    let data: ReconciliationItemDetail[] = [];
    switch (tab) {
      case 'exact':
        data = details.exactMatches;
        break;
      case 'fuzzy':
        data = details.fuzzyMatches;
        break;
      case 'unmatched-bank':
        data = details.unmatchedBankTransactions;
        break;
      case 'unmatched-system':
        data = details.unmatchedSystemTransactions;
        break;
      default:
        return [];
    }
    
    // Sort by date (most recent first)
    return [...data].sort((a, b) => {
      const dateA = a.bankTransaction?.transactionDate || a.systemTransaction?.transactionDate || '';
      const dateB = b.bankTransaction?.transactionDate || b.systemTransaction?.transactionDate || '';
      return new Date(dateB).getTime() - new Date(dateA).getTime();
    });
  };

  const handleManualMatch = async (bankTransaction: BankTransaction) => {
    setSelectedBankTransaction(bankTransaction);
    setLoadingMatches(true);
    
    try {
      // Get potential matches from the unmatched system transactions
      const unmatchedSystem = details?.unmatchedSystemTransactions || [];
      
      // Calculate match confidence for each potential match
      const matches = unmatchedSystem
        .filter(item => item.systemTransaction)
        .map(item => {
          const sysTransaction = item.systemTransaction!;
          
          // Simple confidence calculation (this would ideally use the backend service)
          const amountDiff = Math.abs(bankTransaction.amount - sysTransaction.amount);
          const amountMatch = amountDiff < 0.01;
          
          const bankDate = new Date(bankTransaction.transactionDate);
          const sysDate = new Date(sysTransaction.transactionDate);
          const dateDiff = Math.abs(bankDate.getTime() - sysDate.getTime()) / (1000 * 60 * 60 * 24);
          const dateMatch = dateDiff < 1;
          
          const descSimilarity = calculateDescriptionSimilarity(
            bankTransaction.description, 
            sysTransaction.description
          );
          
          // Weighted confidence score
          const amountScore = amountMatch ? 1.0 : (amountDiff <= 1.0 ? 0.8 : amountDiff <= 5.0 ? 0.6 : 0.3);
          const dateScore = dateDiff === 0 ? 1.0 : dateDiff <= 1 ? 0.9 : dateDiff <= 3 ? 0.7 : 0.4;
          const descScore = descSimilarity;
          
          const confidence = (amountScore * 0.4) + (dateScore * 0.3) + (descScore * 0.3);
          
          return {
            systemTransaction: sysTransaction,
            confidence: confidence,
            matchReasons: {
              amountMatch,
              dateMatch,
              descriptionSimilar: descSimilarity > 0.5,
              amountDifference: amountDiff,
              dateDifferenceInDays: Math.round(dateDiff)
            }
          };
        })
        .sort((a, b) => b.confidence - a.confidence);
      
      setPotentialMatches(matches);
      setShowMatchingModal(true);
    } catch (error) {
      console.error('Failed to load potential matches:', error);
      toast.error(tToasts('reconciliationMatchesLoadFailed'));
    } finally {
      setLoadingMatches(false);
    }
  };

  const calculateDescriptionSimilarity = (desc1: string, desc2: string): number => {
    if (!desc1 || !desc2) return 0;
    
    const normalize = (str: string) => str.toLowerCase().trim();
    const norm1 = normalize(desc1);
    const norm2 = normalize(desc2);
    
    if (norm1 === norm2) return 1.0;
    if (norm1.includes(norm2) || norm2.includes(norm1)) return 0.9;
    
    // Simple word overlap calculation
    const words1 = norm1.split(/\s+/);
    const words2 = norm2.split(/\s+/);
    const commonWords = words1.filter(word => words2.includes(word)).length;
    const totalWords = Math.max(words1.length, words2.length);
    
    return totalWords > 0 ? commonWords / totalWords : 0;
  };

  const handleConfirmMatch = async (bankTransactionId: string, systemTransactionId: number) => {
    try {
      const bankTransaction = selectedBankTransaction;
      if (!bankTransaction) throw new Error('No bank transaction selected');

      await apiClient.request(`/api/reconciliation/${reconciliationId}/manual-match`, {
        method: 'POST',
        body: JSON.stringify({
          systemTransactionId: systemTransactionId,
          bankTransaction: {
            bankTransactionId: bankTransaction.bankTransactionId,
            amount: bankTransaction.amount,
            transactionDate: bankTransaction.transactionDate,
            description: bankTransaction.description,
            bankCategory: bankTransaction.bankCategory
          }
        })
      });
      
      toast.success(tToasts('reconciliationTransactionsMatched'));
      
      // Reload the reconciliation details to reflect the new match
      await loadReconciliationDetails();
      
      setShowMatchingModal(false);
      setSelectedBankTransaction(null);
      setPotentialMatches([]);
    } catch (error) {
      console.error('Failed to match transactions:', error);
      toast.error(tToasts('reconciliationMatchFailed'));
      throw error; // Re-throw to handle in modal
    }
  };

  const handleUnlinkTransaction = async (reconciliationItemId: number) => {
    try {
      await apiClient.request(`/api/reconciliation/items/${reconciliationItemId}/unlink`, {
        method: 'DELETE'
      });
      
      toast.success(tToasts('reconciliationUnlinked'));
      
      // Reload the reconciliation details
      await loadReconciliationDetails();
    } catch (error) {
      console.error('Failed to unlink transaction:', error);
      toast.error(tToasts('reconciliationUnlinkFailed'));
    }
  };

  const handleItemSelection = (itemId: number, selected: boolean) => {
    setSelectedItems(prev => {
      const newSet = new Set(prev);
      if (selected) {
        newSet.add(itemId);
      } else {
        newSet.delete(itemId);
      }
      setShowBulkActions(newSet.size > 0);
      return newSet;
    });
  };

  const handleSelectAll = (items: ReconciliationItemDetail[]) => {
    const fuzzyItems = items;
    const allSelected = fuzzyItems.every(item => selectedItems.has(item.id));
    
    if (allSelected) {
      // Deselect all
      setSelectedItems(new Set());
      setShowBulkActions(false);
    } else {
      // Select all fuzzy items
      const newSelected = new Set(fuzzyItems.map(item => item.id));
      setSelectedItems(newSelected);
      setShowBulkActions(newSelected.size > 0);
    }
  };

  const handleBulkApprove = async (threshold?: number) => {
    setBulkActionLoading(true);
    const itemsToApprove = threshold ? 
      getTabData('fuzzy').filter(item => (item.matchConfidence || 0) >= threshold).map(item => item.id) :
      Array.from(selectedItems);
    
    // Set items as being approved for visual feedback
    setIsApproving(new Set(itemsToApprove));
    
    try {
      const requestData = threshold ?
        { minConfidenceThreshold: threshold } :
        { specificItemIds: itemsToApprove };

      const response = await apiClient.request<{
        approvedCount: number;
        enrichedCount: number;
        categorizedCount: number;
        skippedCount: number;
        errors: string[];
      }>(`/api/reconciliation/${reconciliationId}/bulk-approve`, {
        method: 'POST',
        body: JSON.stringify(requestData)
      });

      // Use actual API response for feedback
      const actualApproved = response.approvedCount;

      if (actualApproved > 0) {
        // Mark items as approved for visual feedback
        setApprovedItems(prev => {
          const newApproved = new Set(prev);
          itemsToApprove.forEach(id => newApproved.add(id));
          return newApproved;
        });

        toast.success(tToasts('reconciliationBulkApproved', { count: actualApproved }));
      } else if (response.skippedCount > 0 || response.errors.length > 0) {
        const errorMsg = response.errors.length > 0
          ? response.errors[0]
          : `${response.skippedCount} items were skipped`;
        toast.error(tToasts('reconciliationApprovalFailed', { message: errorMsg }));
      } else {
        toast.warning(tToasts('reconciliationNoItemsApproved'));
      }
      
      // Show approved state for 2 seconds before removing from view
      setTimeout(() => {
        setSelectedItems(new Set());
        setShowBulkActions(false);
        setApprovedItems(new Set());
        setIsApproving(new Set());
        loadReconciliationDetails();
      }, 2000);
    } catch (error) {
      console.error('Failed to bulk approve matches:', error);
      toast.error(tToasts('reconciliationBulkApproveFailed'));
      setIsApproving(new Set());
    } finally {
      setBulkActionLoading(false);
    }
  };

  const handleBulkDelete = () => {
    setBulkDeleteConfirm({ show: true });
  };

  const confirmBulkDelete = async () => {
    setBulkActionLoading(true);
    const transactionIds = Array.from(selectedItems)
      .map(itemId => {
        const item = getTabData('unmatched-system').find(item => item.id === itemId);
        return item?.systemTransaction?.id;
      })
      .filter((id): id is number => id !== undefined);

    try {
      await apiClient.request('/api/transactions/bulk-delete', {
        method: 'POST',
        body: JSON.stringify({
          transactionIds,
          reason: 'Bulk delete from reconciliation - unmatched system transactions'
        })
      });

      toast.success(tToasts('reconciliationBulkDeleteSuccess', { count: transactionIds.length }));

      // Clear selections and refresh data
      setSelectedItems(new Set());
      setShowBulkActions(false);
      loadReconciliationDetails();
    } catch (error) {
      console.error('Failed to bulk delete transactions:', error);
      toast.error(tToasts('reconciliationBulkDeleteFailed'));
    } finally {
      setBulkActionLoading(false);
      setBulkDeleteConfirm({ show: false });
    }
  };

  const handleBulkImport = () => {
    setBulkImportConfirm({ show: true });
  };

  const handleImportAll = () => {
    // Select all unmatched bank items for import
    const allItems = getTabData('unmatched-bank').map(item => item.id);
    setSelectedItems(new Set(allItems));
    setBulkImportConfirm({ show: true });
  };

  const confirmBulkImport = async () => {
    setBulkActionLoading(true);
    const itemIds = Array.from(selectedItems);

    // Set items as being imported for visual feedback
    setImportingItems(new Set(itemIds));

    try {
      const result = await apiClient.importUnmatchedTransactions(reconciliationId, {
        itemIds: itemIds,
        importAll: false
      });

      // Mark items as imported for visual feedback
      setImportedItems(prev => {
        const newImported = new Set(prev);
        itemIds.forEach(id => newImported.add(id));
        return newImported;
      });

      if (result.importedCount > 0) {
        toast.success(tToasts('reconciliationBulkImportSuccess', { count: result.importedCount }));
      }

      if (result.skippedCount > 0) {
        toast.info(tToasts('reconciliationBulkImportSkipped', { count: result.skippedCount }));
      }

      if (result.errors && result.errors.length > 0) {
        result.errors.forEach((error: string) => toast.error(error));
      }

      // Show imported state for 2 seconds before removing from view
      setTimeout(() => {
        setSelectedItems(new Set());
        setShowBulkActions(false);
        setImportedItems(new Set());
        setImportingItems(new Set());
        loadReconciliationDetails();
      }, 2000);
    } catch (error) {
      console.error('Failed to import transactions:', error);
      toast.error(tToasts('reconciliationBulkImportFailed'));
      setImportingItems(new Set());
    } finally {
      setBulkActionLoading(false);
      setBulkImportConfirm({ show: false });
    }
  };

  const handleSingleImport = async (itemId: number) => {
    setImportingItems(new Set([itemId]));

    try {
      const result = await apiClient.importUnmatchedTransactions(reconciliationId, {
        itemIds: [itemId],
        importAll: false
      });

      if (result.importedCount > 0) {
        setImportedItems(new Set([itemId]));
        toast.success(tToasts('reconciliationTransactionImported'));

        // Show imported state briefly, then refresh
        setTimeout(() => {
          setImportedItems(new Set());
          setImportingItems(new Set());
          loadReconciliationDetails();
        }, 1500);
      } else if (result.skippedCount > 0) {
        toast.info(tToasts('reconciliationTransactionSkipped'));
        setImportingItems(new Set());
      }

      if (result.errors && result.errors.length > 0) {
        result.errors.forEach((error: string) => toast.error(error));
        setImportingItems(new Set());
      }
    } catch (error) {
      console.error('Failed to import transaction:', error);
      toast.error(tToasts('reconciliationTransactionImportFailed'));
      setImportingItems(new Set());
    }
  };

  const handleDragDrop = async (draggedItem: DragDropItem, targetTransaction: BankTransaction | SystemTransaction) => {
    try {
      if (draggedItem.type === 'bank-transaction' && 'id' in targetTransaction) {
        // Bank transaction dropped on system transaction
        const bankTx = draggedItem.data as BankTransaction;
        await handleConfirmMatch(bankTx.bankTransactionId, targetTransaction.id);
      } else if (draggedItem.type === 'system-transaction' && 'bankTransactionId' in targetTransaction) {
        // System transaction dropped on bank transaction  
        const sysTx = draggedItem.data as SystemTransaction;
        await handleConfirmMatch(targetTransaction.bankTransactionId, sysTx.id);
      }
      
      dragDrop.endDrag();
    } catch (error) {
      console.error('Drag and drop match failed:', error);
      dragDrop.endDrag();
    }
  };

  const handleCreateTransaction = (bankTransaction: BankTransaction) => {
    setCreateTransactionBankData(bankTransaction);
    setShowCreateTransactionModal(true);
  };

  const handleTransactionCreated = async (transactionId: number) => {
    try {
      // Auto-match the newly created transaction with the bank transaction
      if (createTransactionBankData) {
        await apiClient.request(`/api/reconciliation/${reconciliationId}/manual-match`, {
          method: 'POST',
          body: JSON.stringify({
            systemTransactionId: transactionId,
            bankTransaction: {
              bankTransactionId: createTransactionBankData.bankTransactionId,
              amount: createTransactionBankData.amount,
              transactionDate: createTransactionBankData.transactionDate,
              description: createTransactionBankData.description,
              bankCategory: createTransactionBankData.bankCategory
            }
          })
        });
        
        toast.success(tToasts('reconciliationCreatedAndMatched'));
      } else {
        toast.success(tToasts('reconciliationCreated'));
      }
    } catch (error) {
      console.error('Failed to auto-match created transaction:', error);
      toast.error(tToasts('reconciliationCreatedMatchFailed'));
    } finally {
      setShowCreateTransactionModal(false);
      setCreateTransactionBankData(null);
      loadReconciliationDetails(); // Refresh the data
    }
  };

  const handleDeleteTransaction = (transactionId: number) => {
    setDeleteConfirm({ show: true, transactionId });
  };

  const confirmDeleteTransaction = async () => {
    if (!deleteConfirm.transactionId) return;

    try {
      await apiClient.request(`/api/transactions/${deleteConfirm.transactionId}`, {
        method: 'DELETE'
      });
      
      toast.success(tToasts('reconciliationTransactionDeleted'));
      loadReconciliationDetails(); // Refresh the data
    } catch (error) {
      console.error('Failed to delete transaction:', error);
      toast.error(tToasts('reconciliationTransactionDeleteFailed'));
    } finally {
      setDeleteConfirm({ show: false });
    }
  };

  const renderTransactionItem = (item: ReconciliationItemDetail) => {
    const config = TAB_CONFIG[activeTab];
    
    // For fuzzy matches with both bank and system transactions, use the detailed comparison component
    if (activeTab === 'fuzzy' && item.bankTransaction && item.systemTransaction && item.matchConfidence) {
      const isSelected = selectedItems.has(item.id);
      const isApproved = approvedItems.has(item.id);
      const isBeingApproved = isApproving.has(item.id);
      
      return (
        <div key={item.id} className={`transition-all duration-300 ${
          isApproved ? 'ring-2 ring-green-400 bg-green-50 rounded-lg p-2 opacity-75' :
          isBeingApproved ? 'ring-2 ring-blue-400 bg-blue-50 rounded-lg p-2 animate-pulse' :
          isSelected ? 'ring-2 ring-primary-300 bg-primary-25 rounded-lg p-2' : ''
        }`}>
          {/* Top control bar */}
          <div className="flex items-center justify-between mb-3">
            <div className="flex items-center gap-3">
              {/* Selection checkbox or approval status */}
              {isApproved ? (
                <div className="flex items-center gap-2">
                  <CheckCircleIcon className="w-4 h-4 text-green-600" />
                  <span className="text-sm font-medium text-green-700">{t('approved')}</span>
                </div>
              ) : isBeingApproved ? (
                <div className="flex items-center gap-2">
                  <div className="w-4 h-4 border-2 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
                  <span className="text-sm font-medium text-blue-700">{t('approving')}</span>
                </div>
              ) : (
                <>
                  <input
                    type="checkbox"
                    checked={isSelected}
                    onChange={(e) => handleItemSelection(item.id, e.target.checked)}
                    className="w-4 h-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500 focus:ring-2"
                  />
                  <span className="text-sm font-medium text-gray-700">
                    {t('fuzzyMatchId', { id: item.id })}
                  </span>
                </>
              )}
            </div>
            
            {/* Action buttons */}
            <div className="flex items-center gap-2">
              {!isApproved && !isBeingApproved && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => handleUnlinkTransaction(item.id)}
                  className="text-red-600 hover:text-red-700 hover:bg-red-50"
                  title={t('unlinkMatch')}
                >
                  <LinkSlashIcon className="w-4 h-4" />
                </Button>
              )}
            </div>
          </div>

          <TransactionComparison
            bankTransaction={item.bankTransaction}
            systemTransaction={item.systemTransaction}
            matchConfidence={item.matchConfidence}
            matchMethod={item.matchMethod}
            expanded={expandedItems.has(item.id)}
            onToggleExpanded={() => toggleExpanded(item.id)}
            className="mb-4"
          />
        </div>
      );
    }
    
    // For unmatched transactions, use the draggable card
    if (activeTab === 'unmatched-bank' || activeTab === 'unmatched-system') {
      const isUnmatchedBank = activeTab === 'unmatched-bank';
      const transaction = isUnmatchedBank ? item.bankTransaction : item.systemTransaction;

      if (!transaction) return null;

      const transactionId = isUnmatchedBank ? item.bankTransaction?.bankTransactionId : item.systemTransaction?.id;
      const isDraggedItem = dragDrop.draggedItem?.id === transactionId;
      const canReceiveDrop = dragDrop.isDragging && !isDraggedItem && dragDrop.canDrop(isUnmatchedBank ? 'system-transaction' : 'bank-transaction');
      const isDragOver = dragDrop.dropZone === `${isUnmatchedBank ? 'bank' : 'system'}-${item.id}`;
      const isImporting = importingItems.has(item.id);
      const isImported = importedItems.has(item.id);

      return (
        <div
          key={item.id}
          className={`transition-all duration-300 ${
            isImported ? 'ring-2 ring-green-400 bg-green-50 rounded-lg opacity-75' :
            isImporting ? 'ring-2 ring-blue-400 bg-blue-50 rounded-lg animate-pulse' : ''
          }`}
        >
          {/* Import status overlay for bank transactions */}
          {isUnmatchedBank && (isImporting || isImported) && (
            <div className="flex items-center gap-2 px-3 py-1 mb-1">
              {isImported ? (
                <>
                  <CheckCircleIcon className="w-4 h-4 text-green-600" />
                  <span className="text-sm font-medium text-green-700">{t('imported')}</span>
                </>
              ) : (
                <>
                  <div className="w-4 h-4 border-2 border-blue-600 border-t-transparent rounded-full animate-spin" />
                  <span className="text-sm font-medium text-blue-700">{t('importingStatus')}</span>
                </>
              )}
            </div>
          )}
          <DraggableTransactionCard
            bankTransaction={isUnmatchedBank ? item.bankTransaction : undefined}
            systemTransaction={!isUnmatchedBank ? item.systemTransaction : undefined}
            bgColor={config.bgColor}
            borderColor={config.borderColor}
            textColor={config.color}
            onDragStart={(dragItem) => {
              dragDrop.startDrag(dragItem);
            }}
            onDragEnd={dragDrop.endDrag}
            onDrop={() => {
              if (dragDrop.draggedItem && transaction) {
                handleDragDrop(dragDrop.draggedItem, transaction);
              }
            }}
            isDragging={isDraggedItem}
            isDragOver={isDragOver}
            canReceiveDrop={canReceiveDrop}
            showMatchButton={isUnmatchedBank && !isImporting && !isImported}
            onMatch={() => isUnmatchedBank && item.bankTransaction && handleManualMatch(item.bankTransaction)}
            showCreateButton={isUnmatchedBank && !isImporting && !isImported}
            onCreateTransaction={() => isUnmatchedBank && item.bankTransaction && handleCreateTransaction(item.bankTransaction)}
            showDeleteButton={!isUnmatchedBank && !!item.systemTransaction}
            onDelete={() => !isUnmatchedBank && item.systemTransaction && handleDeleteTransaction(item.systemTransaction.id)}
            isSelected={(activeTab === 'unmatched-system' || activeTab === 'unmatched-bank') && selectedItems.has(item.id)}
            onSelectionChange={(selected) => (activeTab === 'unmatched-system' || activeTab === 'unmatched-bank') && handleItemSelection(item.id, selected)}
            showImportButton={isUnmatchedBank && !isImporting && !isImported}
            onImport={() => handleSingleImport(item.id)}
            showPreviewButton={isUnmatchedBank && showPreviewButton}
            previewedDescription={isUnmatchedBank && item.bankTransaction ? previewedDescriptions.get(item.bankTransaction.bankTransactionId) : undefined}
            isPreviewing={isUnmatchedBank && item.bankTransaction ? previewingItems.has(item.bankTransaction.bankTransactionId) : false}
            onPreview={() => isUnmatchedBank && item.bankTransaction && handlePreviewDescription(item.bankTransaction.bankTransactionId, item.bankTransaction.description)}
            className="mb-3"
          />
        </div>
      );
    }
    
    // For other transaction types, use the standard card display (exact matches, etc.)
    return (
      <div key={item.id} className={`p-4 border rounded-lg ${config.bgColor} ${config.borderColor} mb-3`}>
        <div className="flex items-start justify-between">
          <div className="flex-1">
            <div className="flex items-center gap-2 mb-2">
              <config.icon className={`w-4 h-4 ${config.color}`} />
              <span className="font-medium text-gray-900">{item.displayDescription}</span>
              <span className={`text-xs px-2 py-1 rounded-full ${config.bgColor} ${config.color} font-medium`}>
                {item.matchTypeLabel}
              </span>
              {item.matchConfidenceLabel && (
                <span className="text-xs px-2 py-1 rounded-full bg-gray-100 text-gray-700">
                  {item.matchConfidenceLabel}
                </span>
              )}
            </div>
            
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-sm">
              <div>
                <span className="text-gray-500">{tCommon('amount')}:</span>
                <span className={`ml-2 font-medium ${
                  (item.systemTransaction?.amount || item.bankTransaction?.amount || 0) >= 0
                    ? 'text-green-600'
                    : 'text-red-600'
                }`}>
                  {item.displayAmount}
                </span>
              </div>

              <div>
                <span className="text-gray-500">{tCommon('date')}:</span>
                <span className="ml-2 text-gray-900">{item.displayDate}</span>
              </div>

              {item.systemTransaction?.categoryName && (
                <div>
                  <span className="text-gray-500">{tCommon('category')}:</span>
                  <span className="ml-2 text-gray-900">{item.systemTransaction.categoryName}</span>
                </div>
              )}

              {item.bankTransaction?.bankCategory && (
                <div>
                  <span className="text-gray-500">{t('bankCategory')}:</span>
                  <span className="ml-2 text-gray-900">{item.bankTransaction.bankCategory}</span>
                </div>
              )}
            </div>

            {/* Additional details for exact matches */}
            {activeTab === 'exact' && item.matchConfidence && (
              <div className="mt-3 p-2 bg-white/50 rounded border border-gray-200">
                <div className="flex items-center gap-4 text-xs text-gray-600">
                  <span>{t('matchConfidencePercent', { percentage: (item.matchConfidence * 100).toFixed(1) })}</span>
                  {item.systemTransaction?.status && (
                    <span>{tCommon('status')}: {getStatusLabel(item.systemTransaction.status)}</span>
                  )}
                </div>
              </div>
            )}
          </div>
          
          {/* Unlink button for exact matches */}
          {activeTab === 'exact' && item.systemTransaction && item.bankTransaction && (
            <div className="flex flex-col gap-2 ml-4">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => handleUnlinkTransaction(item.id)}
                className="text-red-600 hover:text-red-700 hover:bg-red-50"
                title={t('unlinkMatch')}
              >
                <LinkSlashIcon className="w-4 h-4" />
              </Button>
            </div>
          )}
        </div>
      </div>
    );
  };

  // Helper function for status labels
  const getStatusLabel = (status: number): string => {
    switch (status) {
      case 1: return tTransactions('status.pending');
      case 2: return tTransactions('status.cleared');
      case 3: return tTransactions('status.reconciled');
      case 4: return tTransactions('status.cancelled');
      default: return tTransactions('status.unknown');
    }
  };

  if (isLoading || !details) {
    return (
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardContent className="p-6">
          <div className="animate-pulse space-y-4">
            <div className="h-6 bg-gray-200 rounded w-1/4"></div>
            <div className="space-y-3">
              <div className="h-4 bg-gray-200 rounded"></div>
              <div className="h-4 bg-gray-200 rounded w-5/6"></div>
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  const tabData = getTabData(activeTab);
  const tabConfig = TAB_CONFIG[activeTab];

  return (
    <div className="space-y-6">
      {/* Summary Card */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardContent className="p-6">
          <div className="mb-6">
            <h3 className="text-lg font-semibold mb-4">{t('reconciliationReview')}</h3>
            <p className="text-gray-600">
              {t('reviewMatchingResults')}
            </p>
          </div>

          {/* Match Summary */}
          <div className="bg-green-50 border border-green-200 rounded-lg p-4">
            <div className="flex items-center gap-3">
              <CheckCircleIcon className="w-5 h-5 text-green-600" />
              <div>
                <h4 className="font-medium text-green-900">
                  {t('matchRatePercent', { percentage: details.summary.matchPercentage.toFixed(1) })}
                </h4>
                <p className="text-sm text-green-700">
                  {t('transactionsMatchedCount', { matched: details.summary.exactMatches + details.summary.fuzzyMatches, total: details.summary.totalItems })}
                </p>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Balance Cards */}
      <ReconciliationBalanceCards
        statementEndBalance={statementEndBalance}
        unmatchedBankTransactions={details.unmatchedBankTransactions}
        unmatchedSystemTransactions={details.unmatchedSystemTransactions}
      />

      {/* Filters */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardContent className="p-4">
          <div className="flex flex-wrap gap-4 items-end">
            <div className="flex-1 min-w-64">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                {t('searchTransactions')}
              </label>
              <div className="relative">
                <MagnifyingGlassIcon className="w-4 h-4 absolute left-3 top-1/2 transform -translate-y-1/2 text-gray-400" />
                <input
                  type="text"
                  placeholder={t('searchByDescription')}
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="w-full pl-10 px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
                />
              </div>
            </div>
            
            <div className="w-32">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                {t('minAmount')}
              </label>
              <input
                type="number"
                step="0.01"
                placeholder={t('form.amountPlaceholder')}
                value={minAmount}
                onChange={(e) => setMinAmount(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
            </div>
            
            <div className="w-32">
              <label className="block text-sm font-medium text-gray-700 mb-1">
                {t('maxAmount')}
              </label>
              <input
                type="number"
                step="0.01"
                placeholder={t('form.amountPlaceholder')}
                value={maxAmount}
                onChange={(e) => setMaxAmount(e.target.value)}
                className="w-full px-3 py-2 border border-gray-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
              />
            </div>

            <Button
              variant="secondary"
              onClick={() => {
                setSearchTerm('');
                setMinAmount('');
                setMaxAmount('');
              }}
              className="flex items-center gap-2"
            >
              <FunnelIcon className="w-4 h-4" />
              {t('clearFilters')}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Tabs */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardContent className="p-0">
          {/* Tab Headers */}
          <div className="flex border-b border-gray-200">
            {Object.entries(TAB_CONFIG).map(([key, config]) => {
              const count = getTabData(key as TabType).length;
              const isActive = activeTab === key;
              
              return (
                <button
                  key={key}
                  onClick={() => {
                    setActiveTab(key as TabType);
                    // Clear selections when switching tabs
                    setSelectedItems(new Set());
                    setShowBulkActions(false);
                  }}
                  className={`flex items-center gap-2 px-4 py-3 border-b-2 transition-colors ${
                    isActive
                      ? `${config.color} border-current bg-white`
                      : 'text-gray-500 border-transparent hover:text-gray-700 hover:border-gray-300'
                  }`}
                >
                  <config.icon className="w-4 h-4" />
                  <span className="font-medium">{config.label}</span>
                  <span className={`px-2 py-1 text-xs rounded-full ${
                    isActive ? config.bgColor : 'bg-gray-100'
                  }`}>
                    {count}
                  </span>
                </button>
              );
            })}
          </div>

          {/* Bulk Actions Bar */}
          {(activeTab === 'fuzzy' || activeTab === 'unmatched-system' || activeTab === 'unmatched-bank') && (showBulkActions || tabData.length > 0) && (
            <div className="bg-gray-50 border-b border-gray-200 p-4">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                  {/* Select All Checkbox */}
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={selectedItems.size > 0 && selectedItems.size === tabData.length}
                      onChange={() => handleSelectAll(tabData)}
                      className="w-4 h-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500 focus:ring-2"
                    />
                    <span className="text-sm font-medium text-gray-700">
                      {t('selectAllCount', { count: tabData.length })}
                    </span>
                  </label>
                  
                  {selectedItems.size > 0 && (
                    <span className="text-sm text-gray-600">
                      {t('itemsSelected', { count: selectedItems.size })}
                    </span>
                  )}
                </div>

                {/* Bulk Action Buttons */}
                {showBulkActions && (
                  <div className="flex items-center gap-2">
                    {activeTab === 'fuzzy' && (
                      <>
                        {/* Quick Approve Buttons for Fuzzy Matches */}
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => handleBulkApprove(0.9)}
                          disabled={bulkActionLoading}
                          className="text-green-600 border-green-200 hover:bg-green-50"
                        >
                          {t('approveAbovePercent', { percent: 90 })}
                        </Button>

                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={() => handleBulkApprove(0.8)}
                          disabled={bulkActionLoading}
                          className="text-yellow-600 border-yellow-200 hover:bg-yellow-50"
                        >
                          {t('approveAbovePercent', { percent: 80 })}
                        </Button>

                        {/* Approve Selected */}
                        <Button
                          variant="primary"
                          size="sm"
                          onClick={() => handleBulkApprove()}
                          disabled={bulkActionLoading || selectedItems.size === 0}
                          loading={bulkActionLoading}
                          className="flex items-center gap-1"
                        >
                          <CheckCircleIcon className="w-4 h-4" />
                          {t('approveSelectedCount', { count: selectedItems.size })}
                        </Button>
                      </>
                    )}

                    {activeTab === 'unmatched-system' && (
                      <>
                        {/* Bulk Delete Button for Unmatched System */}
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={handleBulkDelete}
                          disabled={bulkActionLoading || selectedItems.size === 0}
                          loading={bulkActionLoading}
                          className="flex items-center gap-1 text-red-600 border-red-200 hover:bg-red-50"
                        >
                          <XMarkIcon className="w-4 h-4" />
                          {t('deleteSelectedCount', { count: selectedItems.size })}
                        </Button>
                      </>
                    )}

                    {activeTab === 'unmatched-bank' && (
                      <>
                        {/* Import All Button */}
                        <Button
                          variant="secondary"
                          size="sm"
                          onClick={handleImportAll}
                          disabled={bulkActionLoading || tabData.length === 0}
                          className="flex items-center gap-1 text-blue-600 border-blue-200 hover:bg-blue-50"
                        >
                          <ArrowDownTrayIcon className="w-4 h-4" />
                          {t('importAllCount', { count: tabData.length })}
                        </Button>

                        {/* Import Selected Button */}
                        <Button
                          variant="primary"
                          size="sm"
                          onClick={handleBulkImport}
                          disabled={bulkActionLoading || selectedItems.size === 0}
                          loading={bulkActionLoading}
                          className="flex items-center gap-1"
                        >
                          <ArrowDownTrayIcon className="w-4 h-4" />
                          {t('importSelectedCount', { count: selectedItems.size })}
                        </Button>
                      </>
                    )}
                    
                    {/* Clear Selection */}
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setSelectedItems(new Set());
                        setShowBulkActions(false);
                      }}
                      className="text-gray-600"
                    >
                      <XMarkIcon className="w-4 h-4" />
                    </Button>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Tab Content */}
          <div className="p-6">
            {/* Drag and Drop Instructions for Unmatched Tabs */}
            {(activeTab === 'unmatched-bank' || activeTab === 'unmatched-system') && tabData.length > 0 && (
              <div className="mb-4 p-3 bg-blue-50 border border-blue-200 rounded-lg">
                <div className="flex items-center gap-2 text-blue-800 text-sm">
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                  <span className="font-medium">{t('quickMatching')}:</span>
                  <span>{t('quickMatchingInstructions')}</span>
                  {dragDrop.isTouchDevice && (
                    <span className="ml-2 text-blue-600">({t('longPressToDrag')})</span>
                  )}
                </div>
              </div>
            )}
            
            {tabData.length === 0 ? (
              <div className="text-center py-12">
                <tabConfig.icon className={`w-12 h-12 mx-auto mb-4 ${tabConfig.color}`} />
                <h3 className="text-lg font-medium text-gray-900 mb-2">
                  {t('noItems', { type: tabConfig.label })}
                </h3>
                <p className="text-gray-500">
                  {searchTerm || minAmount || maxAmount
                    ? t('noTransactionsMatchFilters')
                    : t('noItemsToReview', { type: tabConfig.label.toLowerCase() })}
                </p>
              </div>
            ) : (
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <h4 className="font-medium text-gray-900">
                    {tabConfig.label} ({tabData.length})
                  </h4>
                  <div className="flex items-center gap-4">
                    {/* Drag status indicator */}
                    {dragDrop.isDragging && (
                      <div className="flex items-center gap-2 text-sm text-primary-600 font-medium">
                        <div className="w-2 h-2 bg-primary-600 rounded-full animate-pulse"></div>
                        {t('draggingTransaction')}
                      </div>
                    )}
                    <div className="flex items-center gap-2 text-sm text-gray-500">
                      <ArrowsUpDownIcon className="w-4 h-4" />
                      {t('sortedByDate')}
                    </div>
                  </div>
                </div>
                
                <div className="space-y-3">
                  {tabData.map(renderTransactionItem)}
                </div>
              </div>
            )}
          </div>
        </CardContent>
      </Card>

      {/* Actions */}
      <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
        <CardContent className="p-6">
          <div className="flex justify-end gap-3">
            <Button variant="secondary" onClick={onBack}>
              {t('backToMatching')}
            </Button>
            <Button
              onClick={onCompleteReconciliation}
              disabled={loading}
              className="flex items-center gap-2"
            >
              {loading && <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />}
              {t('completeReconciliation')}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Manual Matching Modal */}
      <ManualMatchingModal
        isOpen={showMatchingModal}
        onClose={() => {
          setShowMatchingModal(false);
          setSelectedBankTransaction(null);
          setPotentialMatches([]);
        }}
        bankTransaction={selectedBankTransaction}
        potentialMatches={potentialMatches}
        onMatch={handleConfirmMatch}
        loading={loadingMatches}
      />

      {/* Create Transaction Modal */}
      {createTransactionBankData && (
        <CreateTransactionModal
          isOpen={showCreateTransactionModal}
          onClose={() => {
            setShowCreateTransactionModal(false);
            setCreateTransactionBankData(null);
          }}
          bankTransaction={createTransactionBankData}
          accountId={accountId}
          onTransactionCreated={handleTransactionCreated}
        />
      )}

      {/* Delete Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={deleteConfirm.show}
        title={t('deleteTransaction')}
        description={t('deleteTransactionConfirm')}
        confirmText={tCommon('delete')}
        cancelText={tCommon('cancel')}
        variant="danger"
        onConfirm={confirmDeleteTransaction}
        onClose={() => setDeleteConfirm({ show: false })}
      />

      {/* Bulk Delete Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={bulkDeleteConfirm.show}
        title={t('deleteMultipleTransactions')}
        description={t('deleteMultipleTransactionsConfirm', { count: selectedItems.size })}
        confirmText={t('deleteTransactionsCount', { count: selectedItems.size })}
        cancelText={tCommon('cancel')}
        variant="danger"
        onConfirm={confirmBulkDelete}
        onClose={() => setBulkDeleteConfirm({ show: false })}
      />

      {/* Bulk Import Confirmation Dialog */}
      <ConfirmationDialog
        isOpen={bulkImportConfirm.show}
        title={t('importBankTransactions')}
        description={t('importBankTransactionsConfirm', { count: selectedItems.size })}
        confirmText={t('importTransactionsCount', { count: selectedItems.size })}
        cancelText={tCommon('cancel')}
        variant="default"
        onConfirm={confirmBulkImport}
        onClose={() => setBulkImportConfirm({ show: false })}
      />
    </div>
  );
}
