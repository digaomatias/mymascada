'use client';

import { Fragment, ReactNode } from 'react';
import { Transition } from '@headlessui/react';
import { useDropdownBehavior } from '@/hooks/useDropdownBehaviorHook';
import { XMarkIcon } from '@heroicons/react/24/outline';
import { useTranslations } from 'next-intl';

interface ResponsiveDropdownProps {
  isOpen: boolean;
  onClose: () => void;
  trigger?: ReactNode;
  title?: string;
  children: ReactNode;
  className?: string;
}

export function ResponsiveDropdown({
  isOpen,
  onClose,
  trigger,
  title,
  children,
  className = '',
}: ResponsiveDropdownProps) {
  const tCommon = useTranslations('common');
  const { dropdownRef, getDropdownStyles, renderBackdrop, isMobile } = useDropdownBehavior({
    isOpen,
    onClose,
  });

  return (
    <>
      {trigger}
      
      {renderBackdrop()}
      
      <Transition
        show={isOpen}
        as={Fragment}
        enter={isMobile ? "" : "transition ease-out duration-100"}
        enterFrom={isMobile ? "" : "transform opacity-0 scale-95"}
        enterTo={isMobile ? "" : "transform opacity-100 scale-100"}
        leave={isMobile ? "" : "transition ease-in duration-75"}
        leaveFrom={isMobile ? "" : "transform opacity-100 scale-100"}
        leaveTo={isMobile ? "" : "transform opacity-0 scale-95"}
      >
        <div
          ref={dropdownRef}
          className={`bg-white shadow-lg overflow-hidden ${
            isMobile ? 'z-50' : 'z-20 rounded-md border border-gray-200'
          } ${className}`}
          style={getDropdownStyles()}
        >
          {isMobile && title && (
            <div className="flex items-center justify-between p-4 border-b">
              <h3 className="text-lg font-semibold">{title}</h3>
              <button
                onClick={onClose}
                className="p-2 rounded-full hover:bg-gray-100"
                aria-label={tCommon('close')}
              >
                <XMarkIcon className="w-5 h-5" />
              </button>
            </div>
          )}
          
          <div className={`overflow-y-auto ${isMobile ? 'mobile-dropdown-content' : ''}`}>
            {children}
          </div>
        </div>
      </Transition>
    </>
  );
}