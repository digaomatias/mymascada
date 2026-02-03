'use client';

import { Badge } from '@/components/ui/badge';
import { useTranslations } from 'next-intl';
import { BackendAccountType, getAccountTypeColor } from '@/lib/utils';

interface AccountTypeBadgeProps {
  type: number;
  className?: string;
}

// Map backend account type to translation key
// NOTE: This function receives BACKEND values (1-based) from the API
const getAccountTypeKey = (backendType: number): string => {
  switch (backendType) {
    case BackendAccountType.Checking: return 'checking';     // 1
    case BackendAccountType.Savings: return 'savings';       // 2
    case BackendAccountType.CreditCard: return 'creditCard'; // 3
    case BackendAccountType.Investment: return 'investment'; // 4
    case BackendAccountType.Loan: return 'loan';             // 5
    case BackendAccountType.Cash: return 'cash';             // 6
    case BackendAccountType.Other: return 'other';           // 99
    default: return 'other';
  }
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
