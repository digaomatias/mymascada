'use client';

import React, { useState } from 'react';
import { Button } from '@/components/ui/button';
import { CheckIcon, EyeIcon } from '@heroicons/react/24/outline';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useTranslations } from 'next-intl';

interface TransactionReviewButtonProps {
  transactionId: number;
  isReviewed: boolean;
  onReviewComplete?: () => void;
  className?: string;
}

export function TransactionReviewButton({ 
  transactionId, 
  isReviewed, 
  onReviewComplete,
  className = ""
}: TransactionReviewButtonProps) {
  const [isLoading, setIsLoading] = useState(false);
  const t = useTranslations('transactions');
  const tToasts = useTranslations('toasts');

  const handleReview = async () => {
    if (isReviewed) return;

    setIsLoading(true);
    try {
      // For now, we'll just mark it as reviewed
      // In the future, we could show a modal with review details
      await apiClient.reviewTransaction(transactionId);
      
      toast.success(tToasts('transactionReviewed'));
      onReviewComplete?.();
    } catch (error) {
      console.error('Failed to review transaction:', error);
      toast.error(tToasts('transactionReviewFailed'));
    } finally {
      setIsLoading(false);
    }
  };

  if (isReviewed) {
    return (
      <div className={`flex items-center gap-1 text-xs text-green-600 ${className}`}>
        <CheckIcon className="w-3 h-3" />
        <span>{t('reviewed')}</span>
      </div>
    );
  }

  return (
    <Button
      variant="ghost"
      size="sm"
      onClick={handleReview}
      disabled={isLoading}
      className={`text-xs px-2 py-1 h-auto text-orange-600 hover:text-orange-700 hover:bg-orange-50 ${className}`}
    >
      <EyeIcon className="w-3 h-3 mr-1" />
      {isLoading ? t('reviewing') : t('review')}
    </Button>
  );
}
