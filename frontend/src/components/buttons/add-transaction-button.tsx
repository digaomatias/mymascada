'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { PlusIcon } from '@heroicons/react/24/outline';
import { AddTransactionModal } from '@/components/modals/add-transaction-modal';
import { useDeviceDetect } from '@/hooks/use-device-detect';
import { useTranslations } from 'next-intl';

interface AddTransactionButtonProps {
  accountId?: string;
  onSuccess?: () => void;
  className?: string;
  children?: React.ReactNode;
}

export function AddTransactionButton({
  accountId,
  onSuccess,
  className = '',
  children
}: AddTransactionButtonProps) {
  const t = useTranslations('transactions');
  const [showModal, setShowModal] = useState(false);
  const { isMobile } = useDeviceDetect();
  const router = useRouter();

  const handleClick = () => {
    if (isMobile) {
      // Navigate to full page on mobile
      const url = accountId 
        ? `/transactions/new?accountId=${accountId}`
        : '/transactions/new';
      router.push(url);
    } else {
      // Show modal on desktop
      setShowModal(true);
    }
  };

  const handleModalSuccess = () => {
    if (onSuccess) {
      onSuccess();
    }
    // Keep modal open for continuous entry
    setShowModal(true);
  };

  return (
    <>
      <Button
        onClick={handleClick}
        className={className}
      >
        <PlusIcon className="w-5 h-5 mr-2" />
        {children || t('addTransaction')}
      </Button>

      {/* Desktop Modal */}
      {!isMobile && (
        <AddTransactionModal
          isOpen={showModal}
          onClose={() => setShowModal(false)}
          accountId={accountId}
          onSuccess={handleModalSuccess}
        />
      )}
    </>
  );
}