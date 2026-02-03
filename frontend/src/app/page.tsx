'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';
import { CurrencyDollarIcon } from '@heroicons/react/24/outline';
import { useAuth } from '@/contexts/auth-context';
import { useTranslations } from 'next-intl';

export default function HomePage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('landing');
  const tAuth = useTranslations('auth');
  const tCommon = useTranslations('common');

  useEffect(() => {
    // Redirect to dashboard if already authenticated
    if (!isLoading && isAuthenticated) {
      router.push('/dashboard');
    }
  }, [isAuthenticated, isLoading, router]);

  // Show loading spinner while checking auth
  if (isLoading) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 flex items-center justify-center">
        <div className="text-center">
          <div className="w-16 h-16 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center mb-4 mx-auto animate-pulse">
            <CurrencyDollarIcon className="w-8 h-8 text-white" />
          </div>
          <p className="text-gray-600">{tCommon('loading')}</p>
        </div>
      </div>
    );
  }

  // Don't render landing page if authenticated (redirect is in progress)
  if (isAuthenticated) {
    return null;
  }
  return (
    <>
      {/* Navigation */}
      <nav className="bg-white/80 backdrop-blur-xs border-b border-primary-100 sticky top-0 z-50">
        <div className="container-responsive">
          <div className="flex justify-between items-center py-4">
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8 bg-gradient-to-br from-primary-500 to-primary-700 rounded-lg shadow-lg flex items-center justify-center">
                <CurrencyDollarIcon className="w-5 h-5 text-white" />
              </div>
              <span className="text-xl font-bold text-primary">{t('appName')}</span>
            </div>
            <div className="flex items-center space-x-4">
              <Link href="/auth/login" className="text-gray-600 hover:text-primary transition-colors">
                {tAuth('signIn')}
              </Link>
              <Link href="/auth/register" className="btn-primary">
                {tAuth('signUp')}
              </Link>
            </div>
          </div>
        </div>
      </nav>

      <main className="min-h-screen bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200">
      <div className="container-responsive py-20">
        <div className="text-center">
          <div className="flex justify-center mb-8">
            <div className="w-24 h-24 bg-gradient-to-br from-primary-500 to-primary-700 rounded-2xl shadow-2xl flex items-center justify-center transform hover:scale-110 transition-all duration-300 animate-pulse">
              <CurrencyDollarIcon className="w-14 h-14 text-white" />
            </div>
          </div>
          <h1 className="text-5xl md:text-6xl font-bold text-gray-900 mb-8 leading-tight">
            {t('heroTitle')} <span className="text-primary">{t('appName')}</span>
          </h1>
          <p className="text-xl text-gray-700 mb-12 max-w-3xl mx-auto leading-relaxed">
            {t('heroSubtitle')}
          </p>
          
          <div className="flex flex-col sm:flex-row gap-6 justify-center">
            <Link 
              href="/auth/login"
              className="btn-primary text-lg px-8 py-4 shadow-lg hover:shadow-xl transform hover:-translate-y-1 transition-all duration-200"
            >
              {tAuth('signIn')}
            </Link>
            <Link 
              href="/auth/register"
              className="btn-secondary text-lg px-8 py-4 shadow-lg hover:shadow-xl transform hover:-translate-y-1 transition-all duration-200"
            >
              {tAuth('signUp')}
            </Link>
          </div>
        </div>

        <div className="mt-24 grid md:grid-cols-3 gap-8">
          <div className="card-hover bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <div className="w-12 h-12 bg-primary-100 rounded-lg flex items-center justify-center mb-4">
              <svg className="w-6 h-6 text-primary" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('features.trackingTitle')}</h3>
            <p className="text-gray-600">
              {t('features.trackingDescription')}
            </p>
          </div>

          <div className="card-hover bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <div className="w-12 h-12 bg-success-100 rounded-lg flex items-center justify-center mb-4">
              <svg className="w-6 h-6 text-success" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('features.insightsTitle')}</h3>
            <p className="text-gray-600">
              {t('features.insightsDescription')}
            </p>
          </div>

          <div className="card-hover bg-white/90 backdrop-blur-xs border-0 shadow-lg">
            <div className="w-12 h-12 bg-info-100 rounded-lg flex items-center justify-center mb-4">
              <svg className="w-6 h-6 text-info" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
            </div>
            <h3 className="text-xl font-semibold text-gray-900 mb-2">{t('features.securityTitle')}</h3>
            <p className="text-gray-600">
              {t('features.securityDescription')}
            </p>
          </div>
        </div>
      </div>
      </main>
    </>
  );
}
