import { 
  ImportCandidate, 
  Transaction, 
  ConflictInfo, 
  ConflictType, 
  ConflictSeverity,
  ConflictReason, 
  ConflictResolution,
  ImportReviewItem,
  TransactionSource
} from '@/types/import-review';

/**
 * Enhanced duplicate detection and import analysis service
 * Provides comprehensive conflict detection for transaction imports
 */
export class ImportAnalysisService {
  private static readonly DEFAULT_DATE_TOLERANCE_DAYS = 3;
  private static readonly DEFAULT_AMOUNT_TOLERANCE = 0.01;
  private static readonly DEFAULT_DESCRIPTION_SIMILARITY_THRESHOLD = 0.8;

  /**
   * Analyzes import candidates and detects conflicts with existing transactions
   */
  static async analyzeImportCandidates(
    candidates: ImportCandidate[],
    existingTransactions: Transaction[],
    options: {
      dateToleranceDays?: number;
      amountTolerance?: number;
      enableTransferDetection?: boolean;
      conflictDetectionLevel?: 'strict' | 'moderate' | 'relaxed';
    } = {}
  ): Promise<ImportReviewItem[]> {
    const {
      dateToleranceDays = this.DEFAULT_DATE_TOLERANCE_DAYS,
      amountTolerance = this.DEFAULT_AMOUNT_TOLERANCE,
      enableTransferDetection = true,
      conflictDetectionLevel = 'moderate'
    } = options;

    return candidates.map((candidate, index) => {
      const conflicts = this.detectConflicts(
        candidate,
        existingTransactions,
        {
          dateToleranceDays,
          amountTolerance,
          enableTransferDetection,
          conflictDetectionLevel
        }
      );

      return {
        id: `import-item-${index}`,
        importCandidate: candidate,
        conflicts,
        reviewDecision: this.suggestInitialDecision(candidate, conflicts),
        isProcessed: false
      };
    });
  }

  /**
   * Detects conflicts between an import candidate and existing transactions
   */
  private static detectConflicts(
    candidate: ImportCandidate,
    existingTransactions: Transaction[],
    options: {
      dateToleranceDays: number;
      amountTolerance: number;
      enableTransferDetection: boolean;
      conflictDetectionLevel: 'strict' | 'moderate' | 'relaxed';
    }
  ): ConflictInfo[] {
    const conflicts: ConflictInfo[] = [];

    for (const existing of existingTransactions) {
      const conflict = this.analyzeTransactionPair(candidate, existing, options);
      if (conflict) {
        conflicts.push(conflict);
      }
    }

    // Sort conflicts by confidence (highest first)
    return conflicts.sort((a, b) => b.confidenceScore - a.confidenceScore);
  }

  /**
   * Analyzes a pair of transactions for potential conflicts
   */
  private static analyzeTransactionPair(
    candidate: ImportCandidate,
    existing: Transaction,
    options: {
      dateToleranceDays: number;
      amountTolerance: number;
      enableTransferDetection: boolean;
      conflictDetectionLevel: 'strict' | 'moderate' | 'relaxed';
    }
  ): ConflictInfo | null {
    const reasons: ConflictReason[] = [];
    let confidence = 0;
    let matchScore = 0;
    let conflictType: ConflictType | null = null;

    // Check for exact duplicates first (highest priority)
    if (this.isExactDuplicate(candidate, existing)) {
      conflictType = ConflictType.ExactDuplicate;
      confidence = 95;
      matchScore = 1.0;
      
      if (candidate.externalId === existing.externalId) {
        reasons.push(ConflictReason.SameExternalId);
      }
      if (candidate.referenceNumber === existing.referenceNumber) {
        reasons.push(ConflictReason.SameReferenceNumber);
      }
      if (this.isSameAmountAndDate(candidate, existing, 0)) {
        reasons.push(ConflictReason.SameAmountAndDate);
      }
    }
    // Check for transfer conflicts
    else if (options.enableTransferDetection && this.isTransferConflict(candidate, existing)) {
      conflictType = ConflictType.TransferConflict;
      confidence = 85;
      matchScore = 0.9;
      reasons.push(ConflictReason.TransferDestinationExists);
    }
    // Check for manual entry conflicts
    else if (this.isManualEntryConflict(candidate, existing, options)) {
      conflictType = ConflictType.ManualEntryConflict;
      confidence = 75;
      matchScore = this.calculateSimilarityScore(candidate, existing);
      reasons.push(ConflictReason.ManualEntryMatch);
    }
    // Check for potential duplicates
    else if (this.isPotentialDuplicate(candidate, existing, options)) {
      conflictType = ConflictType.PotentialDuplicate;
      matchScore = this.calculateSimilarityScore(candidate, existing);
      confidence = Math.round(matchScore * 70); // Scale to 0-70 for potential duplicates

      if (this.isSameAmountAndDate(candidate, existing, options.dateToleranceDays)) {
        reasons.push(ConflictReason.SameAmountAndDate);
      } else if (this.isSimilarAmountNearDate(candidate, existing, options)) {
        reasons.push(ConflictReason.SimilarAmountNearDate);
      }

      if (this.isSimilarDescription(candidate.description, existing.description)) {
        reasons.push(ConflictReason.SimilarDescription);
      }
    }

    if (!conflictType || reasons.length === 0) {
      return null;
    }

    return {
      type: conflictType,
      severity: confidence > 0.8 ? ConflictSeverity.High : confidence > 0.5 ? ConflictSeverity.Medium : ConflictSeverity.Low,
      message: `${conflictType} detected (${Math.round(confidence * 100)}% confidence)`,
      conflictingTransaction: existing.id ? {
        id: existing.id,
        amount: existing.amount,
        transactionDate: existing.transactionDate,
        description: existing.description,
        referenceId: existing.referenceNumber,
        externalReferenceId: existing.externalId,
        source: existing.source,
        status: existing.status,
        createdAt: existing.createdAt
      } : undefined,
      confidenceScore: confidence
    };
  }

  /**
   * Checks if two transactions are exact duplicates
   */
  private static isExactDuplicate(candidate: ImportCandidate, existing: Transaction): boolean {
    // Same external ID (most reliable)
    if (candidate.externalId && existing.externalId && 
        candidate.externalId === existing.externalId) {
      return true;
    }

    // Same reference number
    if (candidate.referenceNumber && existing.referenceNumber && 
        candidate.referenceNumber === existing.referenceNumber) {
      return true;
    }

    // Exact amount, date, and very similar description
    if (this.isSameAmountAndDate(candidate, existing, 0) && 
        this.isSimilarDescription(candidate.description, existing.description, 0.95)) {
      return true;
    }

    return false;
  }

  /**
   * Checks if import candidate conflicts with existing transfer component
   */
  private static isTransferConflict(candidate: ImportCandidate, existing: Transaction): boolean {
    // Check if existing transaction is part of a transfer
    if (!existing.transferId) {
      return false;
    }

    // Check if amounts and dates are very close (potential transfer match)
    return Math.abs(candidate.amount + existing.amount) < 0.01 && // Opposite amounts
           this.isDateWithinTolerance(
             new Date(candidate.date), 
             new Date(existing.transactionDate), 
             1 // 1 day tolerance for transfers
           );
  }

  /**
   * Checks if import candidate conflicts with manual entry
   */
  private static isManualEntryConflict(
    candidate: ImportCandidate, 
    existing: Transaction,
    options: { conflictDetectionLevel: 'strict' | 'moderate' | 'relaxed' }
  ): boolean {
    if (existing.source !== TransactionSource.Manual) {
      return false;
    }

    const dateTolerance = options.conflictDetectionLevel === 'strict' ? 1 : 
                         options.conflictDetectionLevel === 'moderate' ? 2 : 3;

    return this.isSameAmountAndDate(candidate, existing, dateTolerance);
  }

  /**
   * Checks if transactions are potential duplicates
   */
  private static isPotentialDuplicate(
    candidate: ImportCandidate, 
    existing: Transaction,
    options: {
      dateToleranceDays: number;
      amountTolerance: number;
      conflictDetectionLevel: 'strict' | 'moderate' | 'relaxed';
    }
  ): boolean {
    // Don't flag as potential duplicate if we already identified as exact or other conflict type
    if (this.isExactDuplicate(candidate, existing) ||
        this.isTransferConflict(candidate, existing) ||
        this.isManualEntryConflict(candidate, existing, options)) {
      return false;
    }

    return this.isSimilarAmountNearDate(candidate, existing, options) ||
           (this.isSameAmountAndDate(candidate, existing, options.dateToleranceDays) &&
            this.isSimilarDescription(candidate.description, existing.description, 0.7));
  }

  /**
   * Checks if two transactions have the same amount and date within tolerance
   */
  private static isSameAmountAndDate(
    candidate: ImportCandidate, 
    existing: Transaction, 
    dateToleranceDays: number
  ): boolean {
    const amountMatch = Math.abs(candidate.amount - existing.amount) < this.DEFAULT_AMOUNT_TOLERANCE;
    const dateMatch = this.isDateWithinTolerance(
      new Date(candidate.date),
      new Date(existing.transactionDate),
      dateToleranceDays
    );

    return amountMatch && dateMatch;
  }

  /**
   * Checks if transactions have similar amounts and are near in date
   */
  private static isSimilarAmountNearDate(
    candidate: ImportCandidate,
    existing: Transaction,
    options: {
      dateToleranceDays: number;
      amountTolerance: number;
    }
  ): boolean {
    const amountDiff = Math.abs(candidate.amount - existing.amount);
    const amountSimilar = amountDiff <= options.amountTolerance || 
                         amountDiff / Math.max(Math.abs(candidate.amount), Math.abs(existing.amount)) <= 0.05;
    
    const dateClose = this.isDateWithinTolerance(
      new Date(candidate.date),
      new Date(existing.transactionDate),
      options.dateToleranceDays
    );

    return amountSimilar && dateClose;
  }

  /**
   * Checks if two dates are within tolerance
   */
  private static isDateWithinTolerance(date1: Date, date2: Date, toleranceDays: number): boolean {
    const diffMs = Math.abs(date1.getTime() - date2.getTime());
    const diffDays = diffMs / (1000 * 60 * 60 * 24);
    return diffDays <= toleranceDays;
  }

  /**
   * Checks if two descriptions are similar
   */
  private static isSimilarDescription(desc1: string, desc2: string, threshold = 0.8): boolean {
    const similarity = this.calculateStringSimilarity(
      desc1.toLowerCase().trim(),
      desc2.toLowerCase().trim()
    );
    return similarity >= threshold;
  }

  /**
   * Calculates overall similarity score between candidate and existing transaction
   */
  private static calculateSimilarityScore(candidate: ImportCandidate, existing: Transaction): number {
    let score = 0;
    let factors = 0;

    // Amount similarity (40% weight)
    const amountDiff = Math.abs(candidate.amount - existing.amount);
    const maxAmount = Math.max(Math.abs(candidate.amount), Math.abs(existing.amount));
    const amountSimilarity = maxAmount > 0 ? 1 - (amountDiff / maxAmount) : 1;
    score += amountSimilarity * 0.4;
    factors += 0.4;

    // Date similarity (30% weight)
    const dateDiff = Math.abs(
      new Date(candidate.date).getTime() - 
      new Date(existing.transactionDate).getTime()
    );
    const daysDiff = dateDiff / (1000 * 60 * 60 * 24);
    const dateSimilarity = Math.max(0, 1 - (daysDiff / this.DEFAULT_DATE_TOLERANCE_DAYS));
    score += dateSimilarity * 0.3;
    factors += 0.3;

    // Description similarity (30% weight)
    const descSimilarity = this.calculateStringSimilarity(
      candidate.description.toLowerCase(),
      existing.description.toLowerCase()
    );
    score += descSimilarity * 0.3;
    factors += 0.3;

    return factors > 0 ? score / factors : 0;
  }

  /**
   * Calculates string similarity using Levenshtein distance
   */
  private static calculateStringSimilarity(str1: string, str2: string): number {
    if (str1 === str2) return 1;
    if (str1.length === 0 || str2.length === 0) return 0;

    const matrix = Array(str2.length + 1).fill(null).map(() => Array(str1.length + 1).fill(null));

    for (let i = 0; i <= str1.length; i++) matrix[0][i] = i;
    for (let j = 0; j <= str2.length; j++) matrix[j][0] = j;

    for (let j = 1; j <= str2.length; j++) {
      for (let i = 1; i <= str1.length; i++) {
        const substitutionCost = str1[i - 1] === str2[j - 1] ? 0 : 1;
        matrix[j][i] = Math.min(
          matrix[j][i - 1] + 1, // deletion
          matrix[j - 1][i] + 1, // insertion
          matrix[j - 1][i - 1] + substitutionCost // substitution
        );
      }
    }

    const maxLength = Math.max(str1.length, str2.length);
    return 1 - (matrix[str2.length][str1.length] / maxLength);
  }

  /**
   * Suggests initial decision for a review item
   */
  private static suggestInitialDecision(
    candidate: ImportCandidate,
    conflicts: ConflictInfo[]
  ): ConflictResolution {
    if (conflicts.length === 0) {
      return ConflictResolution.Import;
    }

    const highestConfidenceConflict = conflicts[0];

    if (highestConfidenceConflict.type === ConflictType.ExactDuplicate) {
      return ConflictResolution.Skip;
    }

    if (highestConfidenceConflict.confidenceScore >= 0.8) {
      return ConflictResolution.Skip;
    }

    return ConflictResolution.Pending; // Requires manual review
  }

  /**
   * Suggests resolution action for a conflict
   */
  private static suggestResolution(
    conflictType: ConflictType,
    confidence: number
  ): ConflictResolution {
    switch (conflictType) {
      case ConflictType.ExactDuplicate:
        return ConflictResolution.Skip;
      
      case ConflictType.TransferConflict:
        return confidence > 80 ? ConflictResolution.Skip : ConflictResolution.Pending;
      
      case ConflictType.ManualEntryConflict:
        return confidence > 75 ? ConflictResolution.MergeWithExisting : ConflictResolution.Pending;
      
      case ConflictType.PotentialDuplicate:
        if (confidence > 70) return ConflictResolution.Skip;
        if (confidence < 40) return ConflictResolution.Import;
        return ConflictResolution.Pending;
      
      default:
        return ConflictResolution.Pending;
    }
  }

  /**
   * Creates a summary of analysis results
   */
  static createAnalysisSummary(reviewItems: ImportReviewItem[]) {
    return {
      totalCandidates: reviewItems.length,
      cleanImports: reviewItems.filter(item => item.conflicts.length === 0).length,
      exactDuplicates: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
      ).length,
      potentialDuplicates: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.PotentialDuplicate) &&
        !item.conflicts.some(c => c.type === ConflictType.ExactDuplicate)
      ).length,
      transferConflicts: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.TransferConflict)
      ).length,
      manualConflicts: reviewItems.filter(item => 
        item.conflicts.some(c => c.type === ConflictType.ManualEntryConflict)
      ).length,
      requiresReview: reviewItems.filter(item => 
        item.reviewDecision === ConflictResolution.Pending
      ).length
    };
  }
}