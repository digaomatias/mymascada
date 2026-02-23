'use client';

import { useAuth } from '@/contexts/auth-context';
import Link from 'next/link';
import { useRouter, usePathname } from 'next/navigation';
import { useState, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import {
  ChartBarIcon,
  ArrowsRightLeftIcon,
  BuildingOffice2Icon,
  TagIcon,
  CogIcon,
  EllipsisHorizontalIcon,
  AdjustmentsHorizontalIcon,
  WalletIcon,
  ChatBubbleLeftRightIcon,
  FlagIcon,
  ChevronRightIcon,
  ArrowRightOnRectangleIcon,
} from '@heroicons/react/24/outline';
import { AppIcon } from '@/components/app-icon';

export default function Navigation() {
  const { user, logout, isAuthenticated } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const t = useTranslations('nav');
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);

  const handleLogout = () => {
    logout();
    router.push('/');
  };

  // Close mobile menu on route change
  useEffect(() => {
    setIsMobileMenuOpen(false);
  }, [pathname]);

  if (!isAuthenticated) {
    return null;
  }

  // Primary navigation items
  const primaryNavItems = [
    { href: '/dashboard', labelKey: 'dashboard' as const, icon: ChartBarIcon },
    { href: '/transactions', labelKey: 'transactions' as const, icon: ArrowsRightLeftIcon },
    { href: '/budgets', labelKey: 'budgets' as const, icon: WalletIcon },
    { href: '/goals', labelKey: 'goals' as const, icon: FlagIcon },
    { href: '/analytics', labelKey: 'analytics' as const, icon: ChartBarIcon },
    { href: '/chat', labelKey: 'aiChat' as const, icon: ChatBubbleLeftRightIcon },
  ];

  // Management items (below separator)
  const manageItems = [
    { href: '/accounts', labelKey: 'accounts' as const, icon: BuildingOffice2Icon },
    { href: '/categories', labelKey: 'categories' as const, icon: TagIcon },
    { href: '/rules', labelKey: 'rules' as const, icon: AdjustmentsHorizontalIcon },
    { href: '/settings', labelKey: 'settings' as const, icon: CogIcon },
  ];

  // Mobile bottom tab items
  const mobileTabItems = [
    { href: '/dashboard', labelKey: 'dashboard' as const, icon: ChartBarIcon },
    { href: '/transactions', labelKey: 'transactions' as const, icon: ArrowsRightLeftIcon },
    { href: '/budgets', labelKey: 'budgets' as const, icon: WalletIcon },
    { href: '/chat', labelKey: 'aiChat' as const, icon: ChatBubbleLeftRightIcon },
  ];

  // Mobile "More" menu items
  const mobileMoreItems = [
    { href: '/accounts', labelKey: 'accounts' as const, icon: BuildingOffice2Icon },
    { href: '/goals', labelKey: 'goals' as const, icon: FlagIcon },
    { href: '/analytics', labelKey: 'analytics' as const, icon: ChartBarIcon },
    { href: '/categories', labelKey: 'categories' as const, icon: TagIcon },
    { href: '/rules', labelKey: 'rules' as const, icon: AdjustmentsHorizontalIcon },
    { href: '/settings', labelKey: 'settings' as const, icon: CogIcon },
  ];

  const isActiveLink = (href: string) => {
    return pathname === href || pathname.startsWith(href + '/');
  };

  return (
    <>
      {/* ── Desktop Sidebar (lg+) ── */}
      <aside className="hidden lg:flex flex-col fixed inset-y-0 left-0 z-40 w-[260px] bg-gradient-to-b from-[#1a0e3e] via-[#241456] to-[#1a0e3e] border-r border-white/15">
        {/* Logo */}
        <div className="flex items-center gap-3 px-5 pt-6 pb-4">
          <Link href="/dashboard" className="flex items-center gap-3">
            <div className="w-9 h-9">
              <AppIcon size={36} />
            </div>
            <div>
              <span className="text-lg font-bold text-white tracking-tight">MyMascada</span>
              <span className="block text-[10px] font-medium text-violet-300/60 tracking-widest uppercase -mt-0.5">Finance</span>
            </div>
          </Link>
        </div>

        {/* Primary nav */}
        <nav className="flex-1 px-3 pt-2 space-y-1 overflow-y-auto">
          {primaryNavItems.map((item) => {
            const active = isActiveLink(item.href);
            const IconComponent = item.icon;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`nav-link flex items-center gap-2.5 ${
                  active
                    ? 'nav-link-active'
                    : 'text-violet-200/80 hover:bg-white/8 hover:text-white'
                }`}
              >
                <IconComponent className="w-[18px] h-[18px] shrink-0" />
                <span className="flex-1">{t(item.labelKey)}</span>
                {active && <ChevronRightIcon className="w-3.5 h-3.5 opacity-40" />}
              </Link>
            );
          })}

          {/* Separator */}
          <div className="my-3 border-t border-white/10" />

          {/* Manage items */}
          {manageItems.map((item) => {
            const active = isActiveLink(item.href);
            const IconComponent = item.icon;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={`nav-link flex items-center gap-2.5 ${
                  active
                    ? 'nav-link-active'
                    : 'text-violet-200/80 hover:bg-white/8 hover:text-white'
                }`}
              >
                <IconComponent className="w-[18px] h-[18px] shrink-0" />
                <span className="flex-1">{t(item.labelKey)}</span>
                {active && <ChevronRightIcon className="w-3.5 h-3.5 opacity-40" />}
              </Link>
            );
          })}
        </nav>

        {/* User info + logout */}
        <div className="px-4 py-4 border-t border-white/10">
          <div className="text-xs text-violet-300/60 truncate mb-2">
            {user?.firstName || user?.userName || user?.email}
          </div>
          <button
            onClick={handleLogout}
            className="flex items-center gap-2 w-full px-3 py-2 rounded-xl text-[13px] font-medium text-violet-200/80 hover:bg-white/8 hover:text-white transition-colors cursor-pointer"
          >
            <ArrowRightOnRectangleIcon className="w-[18px] h-[18px]" />
            {t('logout')}
          </button>
        </div>
      </aside>

      {/* ── Mobile / Tablet Navigation (< lg) ── */}
      <nav className="lg:hidden">
        {/* Mobile Top Bar */}
        <div className="bg-gradient-to-r from-[#1a0e3e] to-[#241456] shadow-lg">
          <div className="flex justify-between items-center h-14 px-4">
            <Link href="/dashboard" className="flex items-center gap-2">
              <div className="w-8 h-8">
                <AppIcon size={32} />
              </div>
              <span className="text-lg font-bold text-white">MyMascada</span>
            </Link>

            <button
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              className="text-white p-2 rounded-xl hover:bg-white/10 focus:outline-hidden focus:ring-2 focus:ring-white/30 cursor-pointer"
              aria-label={t('toggleMenu')}
            >
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                {isMobileMenuOpen ? (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                ) : (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                )}
              </svg>
            </button>
          </div>
        </div>

        {/* Mobile Slide-out Menu */}
        {isMobileMenuOpen && (
          <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-sm" onClick={() => setIsMobileMenuOpen(false)}>
            <div
              className="fixed right-0 top-0 h-full w-72 bg-gradient-to-b from-[#1a0e3e] via-[#241456] to-[#1a0e3e] shadow-xl"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="px-5 pt-5 pb-3 border-b border-white/10">
                <p className="text-xs text-violet-300/60">{t('welcome', { name: '' }).split(',')[0]}</p>
                <p className="text-sm font-medium text-white truncate">{user?.email}</p>
              </div>

              <div className="p-3 space-y-0.5">
                {[...mobileTabItems, ...mobileMoreItems].map((item) => {
                  const IconComponent = item.icon;
                  const active = isActiveLink(item.href);
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      className={`nav-link flex items-center gap-2.5 ${
                        active
                          ? 'nav-link-active'
                          : 'text-violet-200/80 hover:bg-white/8 hover:text-white'
                      }`}
                      onClick={() => setIsMobileMenuOpen(false)}
                    >
                      <IconComponent className="w-[18px] h-[18px]" />
                      {t(item.labelKey)}
                    </Link>
                  );
                })}
              </div>

              <div className="absolute bottom-0 left-0 right-0 p-4 border-t border-white/10">
                <button
                  onClick={handleLogout}
                  className="flex items-center justify-center gap-2 w-full px-3 py-2.5 rounded-xl text-[13px] font-medium text-violet-200/80 hover:bg-white/8 hover:text-white transition-colors cursor-pointer"
                >
                  <ArrowRightOnRectangleIcon className="w-[18px] h-[18px]" />
                  {t('logout')}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Mobile Bottom Tab Bar */}
        <div className="fixed bottom-0 left-0 right-0 bg-white/95 backdrop-blur-lg border-t border-violet-100/50 shadow-lg z-40 pb-safe">
          <div className="grid grid-cols-5 h-16">
            {mobileTabItems.map((item) => {
              const IconComponent = item.icon;
              const active = isActiveLink(item.href);
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`flex flex-col items-center justify-center py-2 ${
                    active
                      ? 'text-violet-600'
                      : 'text-gray-500 hover:text-violet-600'
                  }`}
                >
                  <IconComponent className="w-5 h-5" />
                  <span className="text-[10px] mt-1 font-medium">{t(item.labelKey)}</span>
                </Link>
              );
            })}

            {/* More button */}
            <button
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              className="flex flex-col items-center justify-center py-2 text-gray-500 hover:text-violet-600 cursor-pointer"
            >
              <EllipsisHorizontalIcon className="w-5 h-5" />
              <span className="text-[10px] mt-1 font-medium">{t('more')}</span>
            </button>
          </div>
        </div>
      </nav>
    </>
  );
}
