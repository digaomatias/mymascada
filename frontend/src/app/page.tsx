'use client';

import { useRouter } from 'next/navigation';
import { useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { AppIcon } from '@/components/app-icon';
import { useTranslations } from 'next-intl';

export default function HomePage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const tCommon = useTranslations('common');

  useEffect(() => {
    if (!isLoading) {
      router.push(isAuthenticated ? '/dashboard' : '/auth/login');
    }
  }, [isAuthenticated, isLoading, router]);

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
      <div className="text-center">
        <div className="w-16 h-16 mb-4 mx-auto animate-pulse">
          <AppIcon size={64} />
        </div>
        <p className="text-gray-600">{tCommon('loading')}</p>
      </div>
    </div>
  );
}
