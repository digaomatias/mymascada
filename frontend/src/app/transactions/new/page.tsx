'use client';

import { useAuth } from '@/contexts/auth-context';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useState, Suspense } from 'react';
import { AppLayout } from '@/components/app-layout';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { TransactionForm, TransactionFormData } from '@/components/forms/transaction-form';
import { apiClient } from '@/lib/api-client';
import Link from 'next/link';
import { toast } from 'sonner';
import { 
  ArrowLeftIcon,
  BanknotesIcon,
  CheckIcon
} from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

function NewTransactionPageContent() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const [success, setSuccess] = useState(false);
  const t = useTranslations('transactions');
  const tCommon = useTranslations('common');

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.push('/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  const handleSubmit = async (formData: TransactionFormData) => {
    // Handle regular transaction (income/expense) - transfer type no longer available
    const amount = parseFloat(formData.amount);
    const finalAmount = formData.type === 'expense' ? -amount : amount;
    
    // Map status to enum values
    const statusMap: Record<string, number> = {
      'pending': 1,
      'cleared': 2, 
      'reconciled': 3,
      'cancelled': 4
    };

    if (!formData.accountId) {
      throw new Error(t('validation.accountRequired'));
    }

    const transactionData = {
      amount: finalAmount,
      transactionDate: new Date(formData.transactionDate).toISOString(),
      description: formData.description.trim(),
      userDescription: formData.userDescription.trim() || undefined,
      accountId: parseInt(formData.accountId), // Required field
      categoryId: formData.categoryId ? parseInt(formData.categoryId) : undefined,
      notes: formData.notes.trim() || undefined,
      location: formData.location.trim() || undefined,
      status: statusMap[formData.status] || 2, // Default to Cleared (2)
    };

    await apiClient.createTransaction(transactionData);
    
    toast.success(t('addedToast', { 
      type: formData.type === 'income' ? t('income') : t('expense'),
      amount: amount.toFixed(2),
      description: formData.description
    }), {
      duration: 4000,
    });

    setSuccess(true);
    
    // Redirect after a brief success message
    setTimeout(() => {
      router.push('/transactions');
    }, 1500);
  };

  if (isLoading) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-violet-500 to-fuchsia-500 rounded-2xl shadow-2xl flex items-center justify-center animate-pulse mx-auto">
            <BanknotesIcon className="w-8 h-8 text-white" />
          </div>
          <div className="mt-6 text-gray-700 font-medium">{tCommon('loading')}</div>
        </div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return null;
  }

  if (success) {
    return (
      <div className="min-h-screen bg-[#faf8ff] flex items-center justify-center">
        <Card className="mx-4 max-w-md w-full bg-white/90 backdrop-blur-xs border-0 shadow-2xl">
          <CardContent className="p-8 text-center">
            <div className="w-16 h-16 bg-gradient-to-br from-success-500 to-success-600 rounded-2xl shadow-2xl flex items-center justify-center mx-auto mb-6">
              <CheckIcon className="w-8 h-8 text-white" />
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mb-2">{t('transactionCreated')}</h2>
            <p className="text-gray-600 mb-6">{t('transactionCreatedDesc')}</p>
            <div className="text-sm text-gray-500">{t('redirectingToTransactions')}</div>
          </CardContent>
        </Card>
      </div>
    );
  }

  const accountIdFromUrl = searchParams.get('accountId') || undefined;

  return (
    <AppLayout>
      {/* Header */}
      <div className="mb-6 lg:mb-8">
        {/* Navigation Bar */}
        <div className="flex items-center justify-between mb-6">
          <Link href="/transactions">
            <Button variant="secondary" size="sm" className="flex items-center gap-2">
              <ArrowLeftIcon className="w-4 h-4" />
              <span className="hidden sm:inline">{t('backToTransactions')}</span>
              <span className="sm:hidden">{tCommon('back')}</span>
            </Button>
          </Link>
        </div>

        {/* Page Title */}
        <div className="text-center mb-8">
          <h1 className="text-2xl sm:text-3xl lg:text-4xl font-bold text-gray-900 mb-2">
            {t('addTransaction')}
          </h1>
          <p className="text-gray-600 text-sm sm:text-base">
            {t('newTransactionDesc')}
          </p>
        </div>
      </div>

      {/* Transaction Form */}
      <div className="max-w-2xl mx-auto">
        <Card className="bg-white/90 backdrop-blur-xs border-0 shadow-lg">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <BanknotesIcon className="w-6 h-6 text-primary-600" />
              {t('transactionDetails')}
            </CardTitle>
          </CardHeader>

          <CardContent className="p-6">
            <TransactionForm
              initialData={accountIdFromUrl ? { accountId: accountIdFromUrl } : undefined}
              onSubmit={handleSubmit}
              onCancel={() => router.push('/transactions')}
            />
          </CardContent>
        </Card>
      </div>
    </AppLayout>
  );
}

export default function NewTransactionPage() {
  const tCommon = useTranslations('common');
  return (
    <Suspense fallback={<div>{tCommon('loading')}</div>}>
      <NewTransactionPageContent />
    </Suspense>
  );
}
