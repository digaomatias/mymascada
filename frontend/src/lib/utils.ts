import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatCurrency(amount: number, currency: string = 'NZD', locale?: string): string {
  return new Intl.NumberFormat(locale || 'en-NZ', {
    style: 'currency',
    currency,
  }).format(amount);
}

export function formatDate(date: string | Date): string {
  try {
    const d = typeof date === 'string' ? new Date(date) : date;
    
    // Check if the date is valid
    if (isNaN(d.getTime())) {
      console.warn('Invalid date provided to formatDate:', date);
      return 'Invalid Date';
    }
    
    return new Intl.DateTimeFormat('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    }).format(d);
  } catch (error) {
    console.error('Error formatting date:', date, error);
    return 'Invalid Date';
  }
}

export function formatDateTime(date: string | Date): string {
  const d = typeof date === 'string' ? new Date(date) : date;
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(d);
}

const monthNameToIndexMap: Record<string, number> = {
  january: 0,
  february: 1,
  march: 2,
  april: 3,
  may: 4,
  june: 5,
  july: 6,
  august: 7,
  september: 8,
  october: 9,
  november: 10,
  december: 11
};

export function formatMonthYearFromName(monthName: string, year: number, locale: string): string {
  const normalized = monthName?.trim().toLowerCase();
  const monthIndex = normalized ? monthNameToIndexMap[normalized] : undefined;

  if (monthIndex === undefined) {
    const fallback = `${monthName} ${year}`.trim();
    return fallback ? fallback[0].toUpperCase() + fallback.slice(1) : fallback;
  }

  const formatted = new Intl.DateTimeFormat(locale, { month: 'long', year: 'numeric' })
    .format(new Date(year, monthIndex, 1));
  return formatted ? formatted[0].toUpperCase() + formatted.slice(1) : formatted;
}

export function classifyAmount(amount: number): 'positive' | 'negative' | 'neutral' {
  if (amount > 0) return 'positive';
  if (amount < 0) return 'negative';
  return 'neutral';
}

// Account Type utilities - Backend enum values start from 1
export const BackendAccountType = {
  Checking: 1,
  Savings: 2,
  CreditCard: 3,
  Investment: 4,
  Loan: 5,
  Cash: 6,
  Other: 99
} as const;

// Frontend AccountType enum for form usage (0-based for compatibility)
export const AccountType = {
  Checking: 0,
  Savings: 1,
  CreditCard: 2,
  Investment: 3,
  Loan: 4,
  Cash: 5
} as const;

// Mapping from 0-based frontend values to 1-based backend values
export const FRONTEND_TO_BACKEND_TYPE: Record<number, number> = {
  0: BackendAccountType.Checking,
  1: BackendAccountType.Savings,
  2: BackendAccountType.CreditCard,
  3: BackendAccountType.Investment,
  4: BackendAccountType.Loan,
  5: BackendAccountType.Cash,
};

// Mapping from backend values (1-based) to display labels
const accountTypeLabels: Record<number, string> = {
  [BackendAccountType.Checking]: 'Checking',
  [BackendAccountType.Savings]: 'Savings',
  [BackendAccountType.CreditCard]: 'Credit Card',
  [BackendAccountType.Investment]: 'Investment',
  [BackendAccountType.Loan]: 'Loan',
  [BackendAccountType.Cash]: 'Cash',
  [BackendAccountType.Other]: 'Other'
} as const;

// Mapping from backend values (1-based) to colors
const accountTypeColors: Record<number, string> = {
  [BackendAccountType.Checking]: 'bg-blue-100 text-blue-800',
  [BackendAccountType.Savings]: 'bg-green-100 text-green-800',
  [BackendAccountType.CreditCard]: 'bg-red-100 text-red-800',
  [BackendAccountType.Investment]: 'bg-purple-100 text-purple-800',
  [BackendAccountType.Loan]: 'bg-yellow-100 text-yellow-800',
  [BackendAccountType.Cash]: 'bg-gray-100 text-gray-800',
  [BackendAccountType.Other]: 'bg-slate-100 text-slate-800'
};

// Convert backend account type to display label
export function getAccountTypeLabel(backendAccountType: number): string {
  return accountTypeLabels[backendAccountType]
    ?? accountTypeLabels[FRONTEND_TO_BACKEND_TYPE[backendAccountType]]
    ?? accountTypeLabels[BackendAccountType.Other];
}

// Convert backend account type to color classes
export function getAccountTypeColor(backendAccountType: number): string {
  return accountTypeColors[backendAccountType]
    ?? accountTypeColors[FRONTEND_TO_BACKEND_TYPE[backendAccountType]]
    ?? accountTypeColors[BackendAccountType.Other];
}

// Convert frontend form enum (0-based) to backend enum (1-based)
export function convertFrontendToBackendAccountType(frontendType: number): number {
  const mapping = {
    [AccountType.Checking]: BackendAccountType.Checking,
    [AccountType.Savings]: BackendAccountType.Savings,
    [AccountType.CreditCard]: BackendAccountType.CreditCard,
    [AccountType.Investment]: BackendAccountType.Investment,
    [AccountType.Loan]: BackendAccountType.Loan,
    [AccountType.Cash]: BackendAccountType.Cash
  };
  return mapping[frontendType as keyof typeof mapping] || BackendAccountType.Checking;
}

// Convert backend enum (1-based) to frontend form enum (0-based)
export function convertBackendToFrontendAccountType(backendType: number): number {
  const mapping = {
    [BackendAccountType.Checking]: AccountType.Checking,
    [BackendAccountType.Savings]: AccountType.Savings,
    [BackendAccountType.CreditCard]: AccountType.CreditCard,
    [BackendAccountType.Investment]: AccountType.Investment,
    [BackendAccountType.Loan]: AccountType.Loan,
    [BackendAccountType.Cash]: AccountType.Cash,
    [BackendAccountType.Other]: AccountType.Cash // Default Other to Cash for form compatibility
  };
  return mapping[backendType as keyof typeof mapping] || AccountType.Checking;
}
