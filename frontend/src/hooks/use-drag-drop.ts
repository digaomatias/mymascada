import { useCallback, useState, useEffect } from 'react';

export interface DragDropItem {
  id: string | number;
  type: 'bank-transaction' | 'system-transaction';
  data: unknown;
}

export interface DragDropState {
  draggedItem: DragDropItem | null;
  dropZone: string | null;
  isDragging: boolean;
}

export function useDragDrop() {
  const [state, setState] = useState<DragDropState>({
    draggedItem: null,
    dropZone: null,
    isDragging: false,
  });
  
  const [isTouchDevice, setIsTouchDevice] = useState(false);
  
  useEffect(() => {
    // Detect touch device
    const checkTouchDevice = () => {
      setIsTouchDevice('ontouchstart' in window || navigator.maxTouchPoints > 0);
    };
    
    checkTouchDevice();
    window.addEventListener('resize', checkTouchDevice);
    
    return () => window.removeEventListener('resize', checkTouchDevice);
  }, []);

  const startDrag = useCallback((item: DragDropItem) => {
    setState({
      draggedItem: item,
      dropZone: null,
      isDragging: true,
    });
  }, []);

  const enterDropZone = useCallback((zoneId: string) => {
    setState(prev => ({
      ...prev,
      dropZone: zoneId,
    }));
  }, []);

  const leaveDropZone = useCallback(() => {
    setState(prev => ({
      ...prev,
      dropZone: null,
    }));
  }, []);

  const endDrag = useCallback(() => {
    setState({
      draggedItem: null,
      dropZone: null,
      isDragging: false,
    });
  }, []);

  const canDrop = useCallback((targetType: string) => {
    if (!state.draggedItem) return false;
    
    // Bank transactions can be dropped on system transactions for matching
    if (state.draggedItem.type === 'bank-transaction' && targetType === 'system-transaction') {
      return true;
    }
    
    // System transactions can be dropped on bank transactions for matching
    if (state.draggedItem.type === 'system-transaction' && targetType === 'bank-transaction') {
      return true;
    }
    
    return false;
  }, [state.draggedItem]);

  return {
    ...state,
    startDrag,
    enterDropZone,
    leaveDropZone,
    endDrag,
    canDrop,
    isTouchDevice,
  };
}