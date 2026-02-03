'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { PlusIcon } from '@heroicons/react/24/outline';
import { AddAccountModal } from '@/components/modals/add-account-modal';
import { useDeviceDetect } from '@/hooks/use-device-detect';
import { useTranslations } from 'next-intl';

interface AddAccountButtonProps {
  onSuccess?: () => void;
  className?: string;
  children?: React.ReactNode;
}

export function AddAccountButton({
  onSuccess,
  className = '',
  children
}: AddAccountButtonProps) {
  const t = useTranslations('accounts');
  const tCommon = useTranslations('common');
  const [showModal, setShowModal] = useState(false);
  const { isMobile } = useDeviceDetect();
  const router = useRouter();

  const handleClick = () => {
    if (isMobile) {
      // Navigate to full page on mobile
      router.push('/accounts/new');
    } else {
      // Show modal on desktop
      setShowModal(true);
    }
  };

  const handleModalSuccess = () => {
    if (onSuccess) {
      onSuccess();
    }
    setShowModal(false);
  };

  return (
    <>
      <Button
        onClick={handleClick}
        className={className}
      >
        <PlusIcon className="w-5 h-5 mr-2" />
        {children || (
          <>
            <span className="hidden sm:inline">{t('addAccount')}</span>
            <span className="sm:hidden">{tCommon('add')}</span>
          </>
        )}
      </Button>

      {/* Desktop Modal */}
      {!isMobile && (
        <AddAccountModal
          isOpen={showModal}
          onClose={() => setShowModal(false)}
          onSuccess={handleModalSuccess}
        />
      )}
    </>
  );
}