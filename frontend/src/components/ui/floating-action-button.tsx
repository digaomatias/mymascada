"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

interface FloatingActionButtonProps {
  onClick: () => void;
  icon?: React.ReactNode;
  label: string;
  className?: string;
}

/**
 * FloatingActionButton - A mobile-only FAB component for primary actions.
 *
 * Features:
 * - Fixed position bottom-right corner
 * - 56px touch target (meets accessibility guidelines)
 * - Hidden on desktop (md:hidden)
 * - Purple gradient matching app theme
 * - Subtle scale animation on press
 * - Full keyboard accessibility
 */
function FloatingActionButton({
  onClick,
  icon,
  label,
  className,
}: FloatingActionButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      className={cn(
        // Position and visibility
        "fixed bottom-6 right-6 z-40 md:hidden",
        // Size - 56px for proper touch target
        "w-14 h-14",
        // Shape and appearance
        "rounded-full",
        "bg-gradient-to-r from-purple-600 to-indigo-600",
        "text-white",
        // Shadow
        "shadow-lg hover:shadow-xl",
        // Focus and accessibility
        "focus:outline-none focus-visible:ring-2 focus-visible:ring-purple-500 focus-visible:ring-offset-2",
        // Animation
        "transition-all duration-200 ease-in-out",
        "active:scale-95 hover:scale-105",
        // Flexbox for centering icon
        "flex items-center justify-center",
        className
      )}
    >
      {icon}
    </button>
  );
}

export { FloatingActionButton };
export type { FloatingActionButtonProps };
