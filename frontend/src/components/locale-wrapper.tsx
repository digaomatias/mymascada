'use client';

import { useAuth } from '@/contexts/auth-context';
import { LocaleProvider } from '@/contexts/locale-context';
import { type ReactNode } from 'react';

interface LocaleWrapperProps {
  children: ReactNode;
}

export function LocaleWrapper({ children }: LocaleWrapperProps) {
  const { user } = useAuth();

  return (
    <LocaleProvider userLocale={user?.locale}>
      {children}
    </LocaleProvider>
  );
}
