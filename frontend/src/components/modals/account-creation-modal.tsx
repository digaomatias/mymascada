'use client';

import { useState } from 'react';
import { XMarkIcon } from '@heroicons/react/24/outline';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import AccountForm, { Account } from '@/components/forms/account-form';
import { apiClient } from '@/lib/api-client';

interface AccountCreationModalProps {
  /** Whether the modal is open */
  isOpen: boolean;
  /** Called when modal should be closed */
  onClose: () => void;
  /** Called when account is successfully created */
  onAccountCreated: (account: Account & { id: number }) => void;
}

export default function AccountCreationModal({
  isOpen,
  onClose,
  onAccountCreated
}: AccountCreationModalProps) {
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (data: Omit<Account, 'id'>) => {
    setLoading(true);
    try {
      const response = await apiClient.createAccount(data);
      
      // Call the success callback with the created account
      onAccountCreated(response as Account & { id: number });
      
      // Close the modal
      onClose();
    } catch (error) {
      console.error('Failed to create account:', error);
      throw error; // Let the form handle the error display
    } finally {
      setLoading(false);
    }
  };

  if (!isOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
      <div className="w-full max-w-lg max-h-[90vh] overflow-auto">
        <Card className="bg-white border-0 shadow-2xl">
          <CardHeader className="relative">
            <button
              onClick={onClose}
              className="absolute right-4 top-4 p-2 rounded-md hover:bg-gray-100 transition-colors"
              disabled={loading}
            >
              <XMarkIcon className="w-5 h-5 text-gray-500" />
            </button>
            <CardTitle className="text-xl font-bold text-gray-900 pr-10">
              Create Account
            </CardTitle>
            <p className="text-sm text-gray-600 mt-1">
              Quickly create a new account to continue adding your transaction.
            </p>
          </CardHeader>
          
          <CardContent>
            <AccountForm
              variant="minimal"
              onSubmit={handleSubmit}
              onCancel={onClose}
              loading={loading}
              submitText="Create Account"
              showCancel={true}
            />
          </CardContent>
        </Card>
      </div>
    </div>
  );
}