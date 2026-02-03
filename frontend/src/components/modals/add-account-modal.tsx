'use client';

import { BaseModal } from './base-modal';
import AccountForm, { Account } from '@/components/forms/account-form';
import { apiClient } from '@/lib/api-client';
import { toast } from 'sonner';
import { useAuth } from '@/contexts/auth-context';

interface AddAccountModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

export function AddAccountModal({
  isOpen,
  onClose,
  onSuccess
}: AddAccountModalProps) {
  const { user } = useAuth();

  const handleSubmit = async (data: Omit<Account, 'id'>) => {
    await apiClient.createAccount(data);
    
    toast.success(`Account created: ${data.name}`, {
      duration: 4000,
    });

    if (onSuccess) {
      onSuccess();
    }
  };

  return (
    <BaseModal
      isOpen={isOpen}
      onClose={onClose}
      title="Add Account"
      size="md"
    >
      <AccountForm
        variant="minimal"
        initialData={{ currency: user?.currency || 'NZD' }}
        onSubmit={handleSubmit}
        onCancel={onClose}
        submitText="Create Account"
        showCancel={true}
      />
    </BaseModal>
  );
}