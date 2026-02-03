'use client';

import { useRef, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Card, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import {
  ArrowsRightLeftIcon,
  LinkSlashIcon,
  PlusIcon,
  TrashIcon,
  ArrowDownTrayIcon
} from '@heroicons/react/24/outline';
import { formatCurrency } from '@/lib/utils';
import { DragDropItem } from '@/hooks/use-drag-drop';

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

interface DraggableTransactionCardProps {
  // Transaction data
  bankTransaction?: BankTransaction;
  systemTransaction?: SystemTransaction;
  
  // Card appearance
  bgColor: string;
  borderColor: string;
  textColor: string;
  
  // Drag and drop
  onDragStart?: (item: DragDropItem) => void;
  onDragEnd?: () => void;
  onDrop?: (draggedItem: DragDropItem) => void;
  isDragOver?: boolean;
  isDragging?: boolean;
  canReceiveDrop?: boolean;
  
  // Actions
  onMatch?: () => void;
  onUnlink?: () => void;
  onCreateTransaction?: () => void;
  onDelete?: () => void;
  onImport?: () => void;

  // Display options
  showMatchButton?: boolean;
  showUnlinkButton?: boolean;
  showCreateButton?: boolean;
  showDeleteButton?: boolean;
  showImportButton?: boolean;
  isSelected?: boolean;
  onSelectionChange?: (selected: boolean) => void;
  
  className?: string;
}

export function DraggableTransactionCard({
  bankTransaction,
  systemTransaction,
  bgColor,
  borderColor,
  onDragStart,
  onDragEnd,
  onDrop,
  isDragOver = false,
  isDragging = false,
  canReceiveDrop = false,
  onMatch,
  onUnlink,
  onCreateTransaction,
  onDelete,
  onImport,
  showMatchButton = false,
  showUnlinkButton = false,
  showCreateButton = false,
  showDeleteButton = false,
  showImportButton = false,
  isSelected = false,
  onSelectionChange,
  className = ''
}: DraggableTransactionCardProps) {
  const t = useTranslations('reconciliation');
  const tCommon = useTranslations('common');
  const cardRef = useRef<HTMLDivElement>(null);
  const [isLongPress, setIsLongPress] = useState(false);
  const longPressTimer = useRef<NodeJS.Timeout | null>(null);

  const transaction = bankTransaction || systemTransaction;
  if (!transaction) return null;

  const handleDragStart = (e: React.DragEvent) => {
    if (!onDragStart) return;
    
    
    const dragItem: DragDropItem = {
      id: bankTransaction ? bankTransaction.bankTransactionId : systemTransaction!.id,
      type: bankTransaction ? 'bank-transaction' : 'system-transaction',
      data: transaction
    };
    
    onDragStart(dragItem);
    
    // Set drag image
    if (cardRef.current) {
      e.dataTransfer.setDragImage(cardRef.current, 0, 0);
    }
    
    e.dataTransfer.effectAllowed = 'move';
  };

  const handleDragEnd = (e: React.DragEvent) => {
    onDragEnd?.();
    e.preventDefault();
  };

  const handleDragOver = (e: React.DragEvent) => {
    if (canReceiveDrop) {
      e.preventDefault();
      e.dataTransfer.dropEffect = 'move';
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    if (onDrop && canReceiveDrop) {
      // The drag item will be provided by the parent component's drag state
      onDrop({
        id: '', // Will be filled by parent
        type: bankTransaction ? 'system-transaction' : 'bank-transaction',
        data: null
      });
    }
  };
  
  // Touch handlers for mobile drag-and-drop simulation
  const handleTouchStart = () => {
    if (!onDragStart) return;
    
    longPressTimer.current = setTimeout(() => {
      setIsLongPress(true);
      const dragItem: DragDropItem = {
        id: bankTransaction ? bankTransaction.bankTransactionId : systemTransaction!.id,
        type: bankTransaction ? 'bank-transaction' : 'system-transaction',
        data: transaction
      };
      onDragStart(dragItem);
    }, 500); // 500ms long press
  };
  
  const handleTouchEnd = () => {
    if (longPressTimer.current) {
      clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
    
    if (isLongPress) {
      setIsLongPress(false);
      onDragEnd?.();
    }
  };
  
  const handleTouchMove = () => {
    if (longPressTimer.current) {
      clearTimeout(longPressTimer.current);
      longPressTimer.current = null;
    }
    
    if (isLongPress && canReceiveDrop) {
      // For simplicity, trigger drop on touch move over drop zone
      onDrop?.({
        id: '',
        type: bankTransaction ? 'system-transaction' : 'bank-transaction',
        data: null
      });
    }
  };

  const cardClasses = `
    relative p-4 border rounded-lg transition-all duration-200
    ${bgColor} ${borderColor}
    ${isDragging ? 'opacity-60 scale-95 rotate-1 shadow-2xl z-50 cursor-grabbing' : onDragStart ? 'cursor-grab hover:shadow-lg' : 'cursor-pointer'}
    ${isDragOver && canReceiveDrop ? 'ring-2 ring-primary-500 ring-opacity-75 scale-105 bg-primary-50 border-primary-300' : ''}
    ${isSelected ? 'ring-2 ring-primary-300 bg-primary-25' : ''}
    ${canReceiveDrop && !isDragging ? 'hover:ring-2 hover:ring-primary-200 hover:bg-primary-25/50' : ''}
    ${className}
  `;

  return (
    <Card
      ref={cardRef}
      className={cardClasses}
      draggable={!!onDragStart}
      onDragStart={handleDragStart}
      onDragEnd={handleDragEnd}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
      onTouchStart={handleTouchStart}
      onTouchEnd={handleTouchEnd}
      onTouchMove={handleTouchMove}
    >
      <CardContent className="p-0">
        {/* Selection checkbox */}
        {onSelectionChange && (
          <div className="absolute top-2 left-2 z-10">
            <input
              type="checkbox"
              checked={isSelected}
              onChange={(e) => onSelectionChange(e.target.checked)}
              onClick={(e) => e.stopPropagation()}
              className="w-4 h-4 text-primary-600 border-gray-300 rounded focus:ring-primary-500 focus:ring-2"
            />
          </div>
        )}

        {/* Drag indicator */}
        {onDragStart && (
          <div className={`absolute top-2 right-2 transition-all duration-200 ${
            isDragging ? 'opacity-0' : 'opacity-50 hover:opacity-100'
          }`}>
            <div className="flex flex-col gap-0.5 p-1">
              <div className="flex gap-0.5">
                <div className="w-1 h-1 bg-gray-400 rounded-full"></div>
                <div className="w-1 h-1 bg-gray-400 rounded-full"></div>
              </div>
              <div className="flex gap-0.5">
                <div className="w-1 h-1 bg-gray-400 rounded-full"></div>
                <div className="w-1 h-1 bg-gray-400 rounded-full"></div>
              </div>
              <div className="flex gap-0.5">
                <div className="w-1 h-1 bg-gray-400 rounded-full"></div>
                <div className="w-1 h-1 bg-gray-400 rounded-full"></div>
              </div>
            </div>
          </div>
        )}

        {/* Drop zone indicator */}
        {isDragOver && canReceiveDrop && (
          <div className="absolute inset-0 bg-primary-100/80 border-2 border-dashed border-primary-500 rounded-lg flex items-center justify-center animate-pulse">
            <div className="text-primary-700 font-semibold flex items-center gap-2">
              <div className="w-2 h-2 bg-primary-600 rounded-full animate-bounce"></div>
              {t('dropToMatch')}
              <div className="w-2 h-2 bg-primary-600 rounded-full animate-bounce" style={{animationDelay: '0.2s'}}></div>
            </div>
          </div>
        )}

        <div className="flex items-start justify-between">
          <div className="flex-1">
            <div className="flex items-center gap-2 mb-2">
              <span className="font-medium text-gray-900">{transaction.description}</span>
              {bankTransaction?.bankCategory && (
                <span className="text-xs px-2 py-1 rounded-full bg-blue-100 text-blue-700">
                  {bankTransaction.bankCategory}
                </span>
              )}
              {systemTransaction?.categoryName && (
                <span className="text-xs px-2 py-1 rounded-full bg-purple-100 text-purple-700">
                  {systemTransaction.categoryName}
                </span>
              )}
            </div>
            
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <span className="text-gray-500">{tCommon('amount')}:</span>
                <span className={`ml-2 font-medium ${
                  transaction.amount >= 0 ? 'text-green-600' : 'text-red-600'
                }`}>
                  {formatCurrency(transaction.amount)}
                </span>
              </div>
              
              <div>
                <span className="text-gray-500">{tCommon('date')}:</span>
                <span className="ml-2 text-gray-900">
                  {new Date(transaction.transactionDate).toLocaleDateString()}
                </span>
              </div>
            </div>
          </div>
          
          {/* Action buttons */}
          <div className="flex flex-col gap-2 ml-4">
            {showMatchButton && onMatch && (
              <Button
                variant="secondary"
                size="sm"
                onClick={(e) => {
                  e.stopPropagation();
                  onMatch();
                }}
                className="flex items-center gap-1 text-primary-600 border-primary-200 hover:bg-primary-50 hover:scale-105 transition-transform duration-200"
              >
                <ArrowsRightLeftIcon className="w-4 h-4" />
                {t('match')}
              </Button>
            )}

            {showCreateButton && onCreateTransaction && (
              <Button
                variant="secondary"
                size="sm"
                onClick={(e) => {
                  e.stopPropagation();
                  onCreateTransaction();
                }}
                className="flex items-center gap-1 text-green-600 border-green-200 hover:bg-green-50 hover:scale-105 transition-transform duration-200"
                title={t('createFromBankImport')}
              >
                <PlusIcon className="w-4 h-4" />
                {tCommon('create')}
              </Button>
            )}

            {showDeleteButton && onDelete && (
              <Button
                variant="ghost"
                size="sm"
                onClick={(e) => {
                  e.stopPropagation();
                  onDelete();
                }}
                className="text-red-600 hover:text-red-700 hover:bg-red-50"
                title={t('deleteThisTransaction')}
              >
                <TrashIcon className="w-4 h-4" />
              </Button>
            )}

            {showUnlinkButton && onUnlink && (
              <Button
                variant="ghost"
                size="sm"
                onClick={(e) => {
                  e.stopPropagation();
                  onUnlink();
                }}
                className="text-red-600 hover:text-red-700 hover:bg-red-50"
                title={t('unlinkMatch')}
              >
                <LinkSlashIcon className="w-4 h-4" />
              </Button>
            )}

            {showImportButton && onImport && (
              <Button
                variant="secondary"
                size="sm"
                onClick={(e) => {
                  e.stopPropagation();
                  onImport();
                }}
                className="flex items-center gap-1 text-blue-600 border-blue-200 hover:bg-blue-50 hover:scale-105 transition-transform duration-200"
                title={t('importAsNewTransaction')}
              >
                <ArrowDownTrayIcon className="w-4 h-4" />
                {tCommon('import')}
              </Button>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}