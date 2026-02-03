'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { PencilIcon } from '@heroicons/react/24/outline';
import { EditTransactionModal } from '@/components/modals/edit-transaction-modal';
import { useDeviceDetect } from '@/hooks/use-device-detect';

interface EditTransactionButtonProps {
  transactionId: string;
  onSuccess?: () => void;
  className?: string;
  children?: React.ReactNode;
}

export function EditTransactionButton({ 
  transactionId, 
  onSuccess,
  className = '',
  children
}: EditTransactionButtonProps) {
  const [showModal, setShowModal] = useState(false);
  const { isMobile } = useDeviceDetect();
  const router = useRouter();

  const handleClick = () => {
    if (isMobile) {
      // Navigate to full page on mobile
      router.push(`/transactions/${transactionId}/edit`);
    } else {
      // Show modal on desktop
      setShowModal(true);
    }
  };

  const handleModalSuccess = () => {
    setShowModal(false);
    if (onSuccess) {
      onSuccess();
    }
  };

  return (
    <>
      <Button 
        onClick={handleClick}
        className={className}
      >
        <PencilIcon className="w-5 h-5 mr-2" />
        {children || 'Edit Transaction'}
      </Button>

      {/* Desktop Modal */}
      {!isMobile && (
        <EditTransactionModal
          isOpen={showModal}
          onClose={() => setShowModal(false)}
          transactionId={transactionId}
          onSuccess={handleModalSuccess}
        />
      )}
    </>
  );
}