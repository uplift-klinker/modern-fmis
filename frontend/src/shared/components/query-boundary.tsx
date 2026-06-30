import type { ReactNode } from 'react';
import { Alert, CircularProgress } from '@mui/material';

interface QueryState<T> {
  data?: T;
  isLoading: boolean;
  isError: boolean;
}

interface QueryBoundaryProps<T> {
  result: QueryState<T>;
  children: (data: T) => ReactNode;
  loadingLabel?: string;
  errorMessage?: string;
}

export function QueryBoundary<T>({
  result,
  children,
  loadingLabel = 'Loading',
  errorMessage = 'Something went wrong. Please try again.',
}: QueryBoundaryProps<T>) {
  if (result.isLoading) {
    return <CircularProgress aria-label={loadingLabel} />;
  }
  if (result.isError) {
    return <Alert severity="error">{errorMessage}</Alert>;
  }
  if (result.data === undefined) {
    return null;
  }
  return <>{children(result.data)}</>;
}
