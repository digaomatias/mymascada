'use client';

import { useAuth } from '@/contexts/auth-context';
import Link from 'next/link';
import { useRouter, usePathname } from 'next/navigation';
import { useState, useRef, useEffect } from 'react';
import { useTranslations } from 'next-intl';
import {
  ChartBarIcon,
  ArrowsRightLeftIcon,
  BuildingOffice2Icon,
  TagIcon,
  CogIcon,
  ChevronDownIcon,
  EllipsisHorizontalIcon,
  AdjustmentsHorizontalIcon,
  WalletIcon
} from '@heroicons/react/24/outline';
import { AppIcon } from '@/components/app-icon';

export default function Navigation() {
  const { user, logout, isAuthenticated } = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const t = useTranslations('nav');
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isManageDropdownOpen, setIsManageDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const handleLogout = () => {
    logout();
    router.push('/');
  };

  // Close dropdown when clicking outside
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsManageDropdownOpen(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  if (!isAuthenticated) {
    return null;
  }

  // Primary navigation items (visible in top bar)
  const primaryNavItems = [
    { href: '/dashboard', labelKey: 'dashboard' as const, icon: ChartBarIcon },
    { href: '/transactions', labelKey: 'transactions' as const, icon: ArrowsRightLeftIcon },
    { href: '/budgets', labelKey: 'budgets' as const, icon: WalletIcon },
    { href: '/analytics', labelKey: 'analytics' as const, icon: ChartBarIcon },
  ];

  // Items under "Manage" dropdown
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
    { href: '/accounts', labelKey: 'accounts' as const, icon: BuildingOffice2Icon },
  ];

  // Mobile "More" menu items
  const mobileMoreItems = [
    { href: '/categories', labelKey: 'categories' as const, icon: TagIcon },
    { href: '/rules', labelKey: 'rules' as const, icon: AdjustmentsHorizontalIcon },
    { href: '/settings', labelKey: 'settings' as const, icon: CogIcon },
  ];

  const isActiveLink = (href: string) => {
    return pathname === href || pathname.startsWith(href + '/');
  };

  const isManageActive = manageItems.some(item => isActiveLink(item.href));

  return (
    <>
      {/* Desktop/Tablet Navigation */}
      <nav className="hidden md:block bg-primary-dark shadow-lg">
        <div className="container-responsive">
          <div className="flex justify-between h-16">
            {/* Logo and Desktop Navigation */}
            <div className="flex items-center">
              <Link href="/dashboard" className="flex items-center space-x-3">
                <div className="w-8 h-8">
                  <AppIcon size={32} />
                </div>
                <span className="text-xl font-bold text-white">MyMascada</span>
              </Link>

              {/* Desktop Navigation Links */}
              <div className="ml-10 flex space-x-2">
                {primaryNavItems.map((item) => {
                  const IconComponent = item.icon;
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      className={`nav-link text-white hover:bg-primary-600 flex items-center ${
                        isActiveLink(item.href) ? 'bg-primary-600' : ''
                      }`}
                    >
                      <IconComponent className="w-4 h-4 mr-2" />
                      {t(item.labelKey)}
                    </Link>
                  );
                })}

                {/* Manage Dropdown */}
                <div className="relative" ref={dropdownRef}>
                  <button
                    onClick={() => setIsManageDropdownOpen(!isManageDropdownOpen)}
                    className={`nav-link text-white hover:bg-primary-600 flex items-center cursor-pointer ${
                      isManageActive ? 'bg-primary-600' : ''
                    }`}
                  >
                    <CogIcon className="w-4 h-4 mr-2" />
                    {t('manage')}
                    <ChevronDownIcon className={`w-4 h-4 ml-1 transition-transform ${
                      isManageDropdownOpen ? 'rotate-180' : ''
                    }`} />
                  </button>

                  {isManageDropdownOpen && (
                    <div className="absolute top-full left-0 mt-1 w-48 bg-white rounded-lg shadow-lg py-1 z-50">
                      {manageItems.map((item) => {
                        const IconComponent = item.icon;
                        return (
                          <Link
                            key={item.href}
                            href={item.href}
                            onClick={() => setIsManageDropdownOpen(false)}
                            className={`flex items-center gap-2 px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 ${
                              isActiveLink(item.href) ? 'bg-primary-50 text-primary-700' : ''
                            }`}
                          >
                            <IconComponent className="w-4 h-4" />
                            {t(item.labelKey)}
                          </Link>
                        );
                      })}
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* Desktop User Menu */}
            <div className="flex items-center space-x-4">
              <span className="text-sm text-primary-200">
                {t('welcome', { name: user?.firstName || user?.userName || '' })}
              </span>
              <button
                onClick={handleLogout}
                className="btn-secondary text-white! border-white! hover:bg-white/10!"
              >
                {t('logout')}
              </button>
            </div>
          </div>
        </div>
      </nav>

      {/* Mobile Navigation */}
      <nav className="md:hidden">
        {/* Mobile Top Bar */}
        <div className="bg-primary-dark shadow-lg">
          <div className="container-responsive">
            <div className="flex justify-between h-16">
              <Link href="/dashboard" className="flex items-center space-x-2">
                <div className="w-8 h-8">
                  <AppIcon size={32} />
                </div>
                <span className="text-lg font-bold text-white">MyMascada</span>
              </Link>

              <button
                onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
                className="text-white p-2 rounded-md hover:bg-primary-600 focus:outline-hidden focus:ring-2 focus:ring-white cursor-pointer"
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
        </div>

        {/* Mobile Slide-out Menu */}
        {isMobileMenuOpen && (
          <div className="fixed inset-0 z-50 bg-black bg-opacity-50" onClick={() => setIsMobileMenuOpen(false)}>
            <div
              className="fixed right-0 top-0 h-full w-64 bg-primary-700 shadow-xl"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="p-4 border-b border-primary-600">
                <p className="text-sm text-primary-200">{t('welcome', { name: '' }).split(',')[0]}</p>
                <p className="text-sm font-medium text-white">{user?.email}</p>
              </div>

              <div className="p-2">
                {[...mobileTabItems, ...mobileMoreItems].map((item) => {
                  const IconComponent = item.icon;
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      className={`nav-link block text-white hover:bg-primary-600 flex items-center mb-1 ${
                        isActiveLink(item.href) ? 'bg-primary-600' : ''
                      }`}
                      onClick={() => setIsMobileMenuOpen(false)}
                    >
                      <IconComponent className="w-4 h-4 mr-2" />
                      {t(item.labelKey)}
                    </Link>
                  );
                })}
              </div>

              <div className="p-4 mt-auto absolute bottom-0 left-0 right-0">
                <button
                  onClick={handleLogout}
                  className="w-full btn-secondary text-white! border-white! hover:bg-white/10!"
                >
                  {t('logout')}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Mobile Bottom Tab Bar */}
        <div className="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 shadow-lg z-40">
          <div className="grid grid-cols-5 h-16">
            {mobileTabItems.map((item) => {
              const IconComponent = item.icon;
              return (
                <Link
                  key={item.href}
                  href={item.href}
                  className={`flex flex-col items-center justify-center py-2 ${
                    isActiveLink(item.href)
                      ? 'text-primary-600'
                      : 'text-gray-600 hover:text-primary-600'
                  }`}
                >
                  <IconComponent className="w-5 h-5" />
                  <span className="text-xs mt-1">{t(item.labelKey)}</span>
                </Link>
              );
            })}

            {/* More button */}
            <button
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
              className="flex flex-col items-center justify-center py-2 text-gray-600 hover:text-primary-600 cursor-pointer"
            >
              <EllipsisHorizontalIcon className="w-5 h-5" />
              <span className="text-xs mt-1">{t('more')}</span>
            </button>
          </div>
        </div>
      </nav>
    </>
  );
}
