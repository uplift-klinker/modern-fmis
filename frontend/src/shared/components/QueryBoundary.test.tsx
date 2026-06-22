import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ThemedShell } from '@/testing/ThemedShell';
import { QueryBoundary } from '@/shared/components/QueryBoundary';

describe('QueryBoundary', () => {
  it('renders a loading indicator while loading', () => {
    render(
      <QueryBoundary result={{ isLoading: true, isError: false }} loadingLabel="Loading widgets">
        {() => <div>loaded</div>}
      </QueryBoundary>,
      { wrapper: ThemedShell },
    );

    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('renders the error message on error', () => {
    render(
      <QueryBoundary result={{ isLoading: false, isError: true }} errorMessage="It broke.">
        {() => <div>loaded</div>}
      </QueryBoundary>,
      { wrapper: ThemedShell },
    );

    expect(screen.getByText('It broke.')).toBeInTheDocument();
  });

  it('renders children with the data when loaded', () => {
    render(
      <QueryBoundary result={{ isLoading: false, isError: false, data: { name: 'Acme' } }}>
        {(data) => <div>{data.name}</div>}
      </QueryBoundary>,
      { wrapper: ThemedShell },
    );

    expect(screen.getByText('Acme')).toBeInTheDocument();
  });
});
