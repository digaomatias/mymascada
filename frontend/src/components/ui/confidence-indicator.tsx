import React from 'react';

interface ConfidenceIndicatorProps {
  confidence: number;
  showPercentage?: boolean;
  size?: 'sm' | 'md' | 'lg';
}

export function ConfidenceIndicator({ 
  confidence, 
  showPercentage = true, 
  size = 'sm' 
}: ConfidenceIndicatorProps) {
  const getConfidenceColor = (confidence: number) => {
    if (confidence >= 0.8) return 'text-green-600 bg-green-100 border-green-200';
    if (confidence >= 0.6) return 'text-yellow-600 bg-yellow-100 border-yellow-200';
    return 'text-red-600 bg-red-100 border-red-200';
  };

  const getConfidenceText = (confidence: number) => {
    if (confidence >= 0.8) return 'High';
    if (confidence >= 0.6) return 'Medium';
    return 'Low';
  };

  const sizeClasses = {
    sm: 'px-2 py-1 text-xs',
    md: 'px-3 py-1.5 text-sm',
    lg: 'px-4 py-2 text-base'
  };

  return (
    <div className={`inline-flex items-center gap-1.5 rounded-full border font-medium ${getConfidenceColor(confidence)} ${sizeClasses[size]}`}>
      <div className="w-2 h-2 rounded-full bg-current" />
      <span>
        {getConfidenceText(confidence)}
        {showPercentage && ` (${Math.round(confidence * 100)}%)`}
      </span>
    </div>
  );
}