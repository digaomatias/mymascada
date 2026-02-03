"use client";

import * as React from "react";
import Link from "next/link";
import { EllipsisVerticalIcon } from "@heroicons/react/24/outline";
import { cn } from "@/lib/utils";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

interface MobileAction {
  id: string;
  label: string;
  icon: React.ReactNode;
  onClick?: () => void;
  href?: string;
  variant?: "default" | "danger";
  show?: boolean;
}

interface MobileActionsOverflowProps {
  actions: MobileAction[];
  triggerClassName?: string;
}

/**
 * MobileActionsOverflow - A dropdown menu for secondary actions on mobile.
 *
 * Features:
 * - Uses existing shadcn/ui DropdownMenu components
 * - Supports both onClick and href (uses Next.js Link for navigation)
 * - Conditional visibility via show prop
 * - Danger variant for destructive actions
 * - Minimum 44px touch target for trigger button
 */
function MobileActionsOverflow({
  actions,
  triggerClassName,
}: MobileActionsOverflowProps) {
  // Filter out actions where show is explicitly false
  const visibleActions = actions.filter((action) => action.show !== false);

  // Don't render if no visible actions
  if (visibleActions.length === 0) {
    return null;
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        className={cn(
          // Minimum 44px touch target for accessibility
          "min-w-[44px] min-h-[44px]",
          // Appearance
          "inline-flex items-center justify-center",
          "rounded-md",
          "bg-transparent hover:bg-gray-100 dark:hover:bg-gray-800",
          "text-gray-700 dark:text-gray-300",
          // Focus and accessibility
          "focus:outline-none focus-visible:ring-2 focus-visible:ring-purple-500 focus-visible:ring-offset-2",
          // Transition
          "transition-colors duration-200",
          triggerClassName
        )}
        aria-label="More actions"
      >
        <EllipsisVerticalIcon className="w-5 h-5" aria-hidden="true" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="min-w-[180px]">
        {visibleActions.map((action) => {
          const itemContent = (
            <>
              <span aria-hidden="true">{action.icon}</span>
              <span>{action.label}</span>
            </>
          );

          // If action has href, render as Link
          if (action.href) {
            return (
              <DropdownMenuItem
                key={action.id}
                asChild
                variant={action.variant === "danger" ? "destructive" : "default"}
              >
                <Link href={action.href} className="flex items-center gap-2">
                  {itemContent}
                </Link>
              </DropdownMenuItem>
            );
          }

          // Otherwise render as button with onClick
          return (
            <DropdownMenuItem
              key={action.id}
              onClick={action.onClick}
              variant={action.variant === "danger" ? "destructive" : "default"}
              className="flex items-center gap-2"
            >
              {itemContent}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export { MobileActionsOverflow };
export type { MobileAction, MobileActionsOverflowProps };
