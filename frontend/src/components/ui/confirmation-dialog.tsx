'use client';

import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { ExclamationTriangleIcon } from '@heroicons/react/24/outline';

interface ConfirmationDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => void;
  title: string;
  description: string;
  confirmText?: string;
  cancelText?: string;
  variant?: 'default' | 'danger';
}

export function ConfirmationDialog({
  isOpen,
  onClose,
  onConfirm,
  title,
  description,
  confirmText = "Confirm",
  cancelText = "Cancel",
  variant = 'default'
}: ConfirmationDialogProps) {
  return (
    <AlertDialog open={isOpen} onOpenChange={onClose}>
      <AlertDialogContent className="max-w-md bg-white shadow-2xl border-0 rounded-2xl p-6">
        <AlertDialogHeader className="pb-4">
          <div className="flex items-start gap-4">
            <div className={`w-12 h-12 rounded-full flex items-center justify-center flex-shrink-0 ${
              variant === 'danger' 
                ? 'bg-red-100 text-red-600' 
                : 'bg-blue-100 text-blue-600'
            }`}>
              <ExclamationTriangleIcon className="w-6 h-6" />
            </div>
            <div className="flex-1">
              <AlertDialogTitle className="text-xl font-semibold text-gray-900 mb-2">
                {title}
              </AlertDialogTitle>
              <AlertDialogDescription className="text-gray-600 leading-relaxed">
                {description}
              </AlertDialogDescription>
            </div>
          </div>
        </AlertDialogHeader>
        <AlertDialogFooter className="pt-6 border-t border-gray-100">
          <div className="flex gap-3 justify-end w-full">
            <Button
              variant="secondary"
              onClick={onClose}
              className="px-6 py-2 bg-gray-100 text-gray-700 hover:bg-gray-200 border-0"
            >
              {cancelText}
            </Button>
            <Button
              onClick={onConfirm}
              className={`px-6 py-2 ${
                variant === 'danger' 
                  ? 'bg-red-600 hover:bg-red-700 text-white' 
                  : 'bg-primary-600 hover:bg-primary-700 text-white'
              } border-0`}
            >
              {confirmText}
            </Button>
          </div>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}