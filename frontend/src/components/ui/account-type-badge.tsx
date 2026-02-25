'use client';

import { Badge } from '@/components/ui/badge';
import { useTranslations } from 'next-intl';
import { BackendAccountType, FRONTEND_TO_BACKEND_TYPE, getAccountTypeColor } from '@/lib/utils';

interface AccountTypeBadgeProps {
  type: number;
  className?: string;
}

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
  return accountTypeKeyMap[type]
    ?? accountTypeKeyMap[FRONTEND_TO_BACKEND_TYPE[type]]
    ?? 'other';
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
