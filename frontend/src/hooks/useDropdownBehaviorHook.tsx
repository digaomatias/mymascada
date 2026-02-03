import React, { useEffect, useRef, useState } from 'react';
import { useDeviceDetect } from './use-device-detect';

interface UseDropdownBehaviorProps {
  isOpen: boolean;
  onClose: () => void;
  closeOnClickOutside?: boolean;
  closeOnEscape?: boolean;
}

export function useDropdownBehavior({
  isOpen,
  onClose,
  closeOnClickOutside = true,
  closeOnEscape = true,
}: UseDropdownBehaviorProps) {
  const { isMobile } = useDeviceDetect();
  const dropdownRef = useRef<HTMLDivElement>(null);
  const [scrollPosition, setScrollPosition] = useState(0);

  useEffect(() => {
    if (!isOpen) return;

    // Handle body scroll lock on mobile
    if (isMobile) {
      // Save current scroll position
      setScrollPosition(window.scrollY);
      
      // Lock body scroll
      document.body.classList.add('dropdown-open');
      document.body.style.top = `-${window.scrollY}px`;
      
      return () => {
        // Restore body scroll
        document.body.classList.remove('dropdown-open');
        document.body.style.top = '';
        window.scrollTo(0, scrollPosition);
      };
    }
  }, [isOpen, isMobile, scrollPosition]);

  useEffect(() => {
    if (!isOpen) return;

    const handleClickOutside = (event: MouseEvent | TouchEvent) => {
      if (!closeOnClickOutside) return;
      
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        // On mobile, only close if clicking the overlay backdrop
        if (isMobile) {
          const target = event.target as HTMLElement;
          if (target.classList.contains('dropdown-backdrop')) {
            onClose();
          }
        } else {
          onClose();
        }
      }
    };

    const handleEscape = (event: KeyboardEvent) => {
      if (!closeOnEscape) return;
      
      if (event.key === 'Escape') {
        onClose();
      }
    };

    // Use appropriate events for each platform
    const clickEvent = isMobile ? 'touchstart' : 'mousedown';
    
    document.addEventListener(clickEvent, handleClickOutside as EventListener);
    document.addEventListener('keydown', handleEscape);

    return () => {
      document.removeEventListener(clickEvent, handleClickOutside as EventListener);
      document.removeEventListener('keydown', handleEscape);
    };
  }, [isOpen, onClose, closeOnClickOutside, closeOnEscape, isMobile]);

  // Platform-specific styles
  const getDropdownStyles = () => {
    if (isMobile) {
      return {
        position: 'fixed' as const,
        bottom: 0,
        left: 0,
        right: 0,
        maxHeight: '70vh',
        borderTopLeftRadius: '16px',
        borderTopRightRadius: '16px',
        animation: isOpen ? 'mobileSlideUp 0.2s ease-out' : undefined,
      };
    }
    
    return {
      position: 'absolute' as const,
      marginTop: '4px',
      width: '100%',
      maxHeight: '320px',
    };
  };

  // Platform-specific backdrop
  const renderBackdrop = () => {
    if (!isOpen || !isMobile) return null;
    
    return (
      <div 
        className="dropdown-backdrop fixed inset-0 bg-black/40 z-40"
        aria-hidden="true"
      />
    );
  };

  return {
    dropdownRef,
    getDropdownStyles,
    renderBackdrop,
    isMobile,
  };
}