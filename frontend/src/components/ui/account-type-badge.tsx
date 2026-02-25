'use client';

import { Badge } from '@/components/ui/badge';
import { useTranslations } from 'next-intl';
import { BackendAccountType, getAccountTypeColor } from '@/lib/utils';

interface AccountTypeBadgeProps {
  type: number;
  className?: string;
}

// Mapping from 0-based frontend values to 1-based backend values
const frontendToBackend: Record<number, number> = {
  0: BackendAccountType.Checking,
  1: BackendAccountType.Savings,
  2: BackendAccountType.CreditCard,
  3: BackendAccountType.Investment,
  4: BackendAccountType.Loan,
  5: BackendAccountType.Cash,
};

// Map from 1-based backend values to translation keys
const accountTypeKeyMap: Record<number, string> = {
  [BackendAccountType.Checking]: 'checking',
  [BackendAccountType.Savings]: 'savings',
  [BackendAccountType.CreditCard]: 'creditCard',
  [BackendAccountType.Investment]: 'investment',
  [BackendAccountType.Loan]: 'loan',
  [BackendAccountType.Cash]: 'cash',
  [BackendAccountType.Other]: 'other',
};

// Map account type to translation key, handling both 0-based and 1-based values
const getAccountTypeKey = (type: number): string => {
  // Try direct lookup (1-based backend values)
  const key = accountTypeKeyMap[type];
  if (key) return key;

  // Fallback: try 0-based frontend values
  const backendType = frontendToBackend[type];
  if (backendType !== undefined) return accountTypeKeyMap[backendType];

  return 'other';
};

export function AccountTypeBadge({ type, className = '' }: AccountTypeBadgeProps) {
  const t = useTranslations('accounts.types');
  const colorClasses = getAccountTypeColor(type);
  const typeKey = getAccountTypeKey(type);

  return (
    <Badge className={`${colorClasses} ${className}`}>
      {t(typeKey)}
    </Badge>
  );
}
