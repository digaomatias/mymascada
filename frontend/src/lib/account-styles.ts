import {
  BuildingOffice2Icon,
  CreditCardIcon,
  BanknotesIcon,
  CurrencyDollarIcon,
  WalletIcon,
  ChartBarIcon,
  BuildingLibraryIcon,
} from '@heroicons/react/24/outline';
import { BackendAccountType, FRONTEND_TO_BACKEND_TYPE } from './utils';

/** Gradient + icon config per account type (backend 1-based enum values). */
export const ACCOUNT_TYPE_STYLES: Record<number, { gradient: string; icon: typeof BuildingOffice2Icon }> = {
  [BackendAccountType.Checking]: { gradient: 'from-blue-500 to-blue-600', icon: BuildingLibraryIcon },
  [BackendAccountType.Savings]: { gradient: 'from-emerald-500 to-emerald-600', icon: BanknotesIcon },
  [BackendAccountType.CreditCard]: { gradient: 'from-rose-500 to-rose-600', icon: CreditCardIcon },
  [BackendAccountType.Investment]: { gradient: 'from-violet-500 to-fuchsia-500', icon: ChartBarIcon },
  [BackendAccountType.Loan]: { gradient: 'from-amber-500 to-amber-600', icon: CurrencyDollarIcon },
  [BackendAccountType.Cash]: { gradient: 'from-slate-500 to-slate-600', icon: WalletIcon },
  [BackendAccountType.Other]: { gradient: 'from-slate-400 to-slate-500', icon: BuildingOffice2Icon },
};

export function getAccountTypeStyle(type: number) {
  return ACCOUNT_TYPE_STYLES[type]
    ?? ACCOUNT_TYPE_STYLES[FRONTEND_TO_BACKEND_TYPE[type]]
    ?? ACCOUNT_TYPE_STYLES[BackendAccountType.Other];
}
