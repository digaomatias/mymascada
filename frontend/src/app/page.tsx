'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';
import { useAuth } from '@/contexts/auth-context';
import { AppIcon } from '@/components/app-icon';
import { useTranslations } from 'next-intl';

export default function HomePage() {
  const { isAuthenticated, isLoading } = useAuth();
  const router = useRouter();
  const t = useTranslations('landing');
  const tAuth = useTranslations('auth');
  const tCommon = useTranslations('common');

  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      router.push('/dashboard');
    }
  }, [isAuthenticated, isLoading, router]);

  if (isLoading) {
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

  if (isAuthenticated) {
    return null;
  }

  return (
    <>
      {/* Navigation */}
      <nav className="bg-white/80 backdrop-blur-sm border-b border-primary-100 sticky top-0 z-50">
        <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center py-4">
            <div className="flex items-center space-x-3">
              <div className="w-8 h-8">
                <AppIcon size={32} />
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

      <main className="min-h-screen">
        {/* Hero Section */}
        <section className="bg-gradient-to-br from-primary-100 via-purple-50 to-primary-200 py-20 md:py-28">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center max-w-4xl mx-auto">
              <div className="flex justify-center mb-8">
                <div className="w-20 h-20 transform hover:scale-110 transition-all duration-300">
                  <AppIcon size={80} />
                </div>
              </div>
              <h1 className="text-4xl md:text-6xl font-bold text-gray-900 mb-6 leading-tight">
                {t('hero.titleLine1')}{' '}
                <span className="text-primary">{t('hero.titleHighlight')}</span>
                {t('hero.titleLine2') && (
                  <span className="block mt-2">{t('hero.titleLine2')}</span>
                )}
              </h1>
              <p className="text-lg md:text-xl text-gray-700 mb-8 max-w-2xl mx-auto leading-relaxed">
                {t('hero.subtitle')}
              </p>
              <p className="text-base text-gray-500 mb-10 max-w-xl mx-auto">
                {t('hero.subtext')}
              </p>

              <div className="flex flex-col sm:flex-row gap-4 justify-center">
                <Link
                  href="/auth/register"
                  className="btn-primary text-lg px-8 py-4 shadow-lg hover:shadow-xl transform hover:-translate-y-1 transition-all duration-200"
                >
                  {t('hero.ctaPrimary')}
                </Link>
                <Link
                  href="#features"
                  className="btn-secondary text-lg px-8 py-4 shadow-lg hover:shadow-xl transform hover:-translate-y-1 transition-all duration-200"
                >
                  {t('hero.ctaSecondary')}
                </Link>
              </div>
            </div>
          </div>
        </section>

        {/* Social Proof / Stats */}
        <section className="bg-white py-12 border-b border-gray-100">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="grid grid-cols-2 md:grid-cols-4 gap-8 text-center">
              {[
                { key: 'stat1' },
                { key: 'stat2' },
                { key: 'stat3' },
                { key: 'stat4' },
              ].map(({ key }) => (
                <div key={key}>
                  <div className="text-2xl md:text-3xl font-bold text-primary mb-1">
                    {t(`stats.${key}.value`)}
                  </div>
                  <div className="text-sm text-gray-500">{t(`stats.${key}.label`)}</div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Features Section */}
        <section id="features" className="py-20 bg-gray-50">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center mb-16">
              <h2 className="text-3xl md:text-4xl font-bold text-gray-900 mb-4">
                {t('features.sectionTitle')}
              </h2>
              <p className="text-lg text-gray-600 max-w-2xl mx-auto">
                {t('features.sectionSubtitle')}
              </p>
            </div>

            <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-8">
              {/* Feature 1: Tracking */}
              <div className="bg-white rounded-xl p-8 shadow-sm hover:shadow-md transition-shadow">
                <div className="w-12 h-12 bg-primary-100 rounded-lg flex items-center justify-center mb-5">
                  <svg className="w-6 h-6 text-primary" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                  </svg>
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-3">{t('features.tracking.title')}</h3>
                <p className="text-gray-600 leading-relaxed">{t('features.tracking.description')}</p>
              </div>

              {/* Feature 2: AI */}
              <div className="bg-white rounded-xl p-8 shadow-sm hover:shadow-md transition-shadow">
                <div className="w-12 h-12 bg-violet-100 rounded-lg flex items-center justify-center mb-5">
                  <svg className="w-6 h-6 text-violet-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
                  </svg>
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-3">{t('features.ai.title')}</h3>
                <p className="text-gray-600 leading-relaxed">{t('features.ai.description')}</p>
              </div>

              {/* Feature 3: Budgets & Goals */}
              <div className="bg-white rounded-xl p-8 shadow-sm hover:shadow-md transition-shadow">
                <div className="w-12 h-12 bg-green-100 rounded-lg flex items-center justify-center mb-5">
                  <svg className="w-6 h-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6" />
                  </svg>
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-3">{t('features.goals.title')}</h3>
                <p className="text-gray-600 leading-relaxed">{t('features.goals.description')}</p>
              </div>

              {/* Feature 4: Import */}
              <div className="bg-white rounded-xl p-8 shadow-sm hover:shadow-md transition-shadow">
                <div className="w-12 h-12 bg-blue-100 rounded-lg flex items-center justify-center mb-5">
                  <svg className="w-6 h-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                  </svg>
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-3">{t('features.import.title')}</h3>
                <p className="text-gray-600 leading-relaxed">{t('features.import.description')}</p>
              </div>

              {/* Feature 5: Analytics */}
              <div className="bg-white rounded-xl p-8 shadow-sm hover:shadow-md transition-shadow">
                <div className="w-12 h-12 bg-amber-100 rounded-lg flex items-center justify-center mb-5">
                  <svg className="w-6 h-6 text-amber-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16 8v8m-4-5v5m-4-2v2m-2 4h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                  </svg>
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-3">{t('features.analytics.title')}</h3>
                <p className="text-gray-600 leading-relaxed">{t('features.analytics.description')}</p>
              </div>

              {/* Feature 6: Security */}
              <div className="bg-white rounded-xl p-8 shadow-sm hover:shadow-md transition-shadow">
                <div className="w-12 h-12 bg-rose-100 rounded-lg flex items-center justify-center mb-5">
                  <svg className="w-6 h-6 text-rose-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
                  </svg>
                </div>
                <h3 className="text-xl font-semibold text-gray-900 mb-3">{t('features.security.title')}</h3>
                <p className="text-gray-600 leading-relaxed">{t('features.security.description')}</p>
              </div>
            </div>
          </div>
        </section>

        {/* Education / Value Proposition Section */}
        <section className="py-20 bg-white">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="md:grid md:grid-cols-2 md:gap-16 items-center">
              <div>
                <h2 className="text-3xl md:text-4xl font-bold text-gray-900 mb-6">
                  {t('valueProposition.title')}
                </h2>
                <p className="text-lg text-gray-600 mb-6 leading-relaxed">
                  {t('valueProposition.paragraph1')}
                </p>
                <p className="text-lg text-gray-600 mb-8 leading-relaxed">
                  {t('valueProposition.paragraph2')}
                </p>
                <ul className="space-y-4">
                  {['point1', 'point2', 'point3', 'point4'].map((point) => (
                    <li key={point} className="flex items-start gap-3">
                      <svg className="w-6 h-6 text-primary flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                      </svg>
                      <span className="text-gray-700">{t(`valueProposition.${point}`)}</span>
                    </li>
                  ))}
                </ul>
              </div>
              <div className="mt-12 md:mt-0">
                <div className="bg-gradient-to-br from-primary-50 to-violet-100 rounded-2xl p-8 md:p-12">
                  <div className="text-center">
                    <div className="text-6xl mb-6">💡</div>
                    <blockquote className="text-xl md:text-2xl font-medium text-gray-800 italic leading-relaxed">
                      &ldquo;{t('valueProposition.quote')}&rdquo;
                    </blockquote>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* How It Works */}
        <section className="py-20 bg-gray-50">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="text-center mb-16">
              <h2 className="text-3xl md:text-4xl font-bold text-gray-900 mb-4">
                {t('howItWorks.title')}
              </h2>
            </div>
            <div className="grid md:grid-cols-3 gap-8">
              {['step1', 'step2', 'step3'].map((step, index) => (
                <div key={step} className="text-center">
                  <div className="w-14 h-14 bg-primary text-white rounded-full flex items-center justify-center text-xl font-bold mx-auto mb-5">
                    {index + 1}
                  </div>
                  <h3 className="text-xl font-semibold text-gray-900 mb-3">
                    {t(`howItWorks.${step}.title`)}
                  </h3>
                  <p className="text-gray-600 leading-relaxed">
                    {t(`howItWorks.${step}.description`)}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Open Source / Self-Host Section */}
        <section className="py-20 bg-white">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
            <div className="max-w-3xl mx-auto">
              <div className="text-4xl mb-6">🔓</div>
              <h2 className="text-3xl md:text-4xl font-bold text-gray-900 mb-6">
                {t('openSource.title')}
              </h2>
              <p className="text-lg text-gray-600 mb-8 leading-relaxed">
                {t('openSource.description')}
              </p>
              <div className="flex flex-col sm:flex-row gap-4 justify-center">
                <a
                  href="https://github.com/digaomatias/mymascada"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="btn-secondary text-lg px-8 py-4 inline-flex items-center justify-center gap-2"
                >
                  <svg className="w-5 h-5" fill="currentColor" viewBox="0 0 24 24">
                    <path fillRule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.531 1.032 1.531 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" clipRule="evenodd" />
                  </svg>
                  GitHub
                </a>
              </div>
            </div>
          </div>
        </section>

        {/* Final CTA */}
        <section className="py-20 bg-gradient-to-br from-primary-600 to-violet-700">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8 text-center">
            <h2 className="text-3xl md:text-4xl font-bold text-white mb-6">
              {t('cta.title')}
            </h2>
            <p className="text-lg text-primary-100 mb-10 max-w-2xl mx-auto">
              {t('cta.subtitle')}
            </p>
            <Link
              href="/auth/register"
              className="bg-white text-primary-700 font-semibold text-lg px-10 py-4 rounded-lg shadow-lg hover:shadow-xl transform hover:-translate-y-1 transition-all duration-200 inline-block"
            >
              {t('cta.button')}
            </Link>
          </div>
        </section>

        {/* Footer */}
        <footer className="bg-gray-900 text-gray-400 py-12">
          <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
            <div className="flex flex-col md:flex-row justify-between items-center gap-6">
              <div className="flex items-center space-x-3">
                <div className="w-6 h-6">
                  <AppIcon size={24} />
                </div>
                <span className="text-white font-semibold">{t('appName')}</span>
              </div>
              <div className="flex gap-6 text-sm">
                <Link href="/terms" className="hover:text-white transition-colors">
                  {t('footer.terms')}
                </Link>
                <Link href="/privacy" className="hover:text-white transition-colors">
                  {t('footer.privacy')}
                </Link>
                <a
                  href="https://github.com/digaomatias/mymascada"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="hover:text-white transition-colors"
                >
                  GitHub
                </a>
              </div>
              <p className="text-sm">{t('footer.copyright')}</p>
            </div>
          </div>
        </footer>
      </main>
    </>
  );
}
