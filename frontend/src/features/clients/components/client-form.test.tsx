import { describe, it, expect, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithProviders } from '@/testing/render-with-providers';
import { ClientForm } from '@/features/clients/components/client-form';

describe('ClientForm', () => {
  it('submits the entered client when valid', async () => {
    const onSubmit = vi.fn();
    renderWithProviders(<ClientForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(/name/i), 'Acme Farms');
    await userEvent.type(screen.getByLabelText(/email/i), 'ops@acme.example');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    await waitFor(() =>
      expect(onSubmit).toHaveBeenCalledWith(
        expect.objectContaining({ name: 'Acme Farms', email: 'ops@acme.example' }),
      ),
    );
  });

  it('blocks submit and shows an error when neither email nor phone is provided', async () => {
    const onSubmit = vi.fn();
    renderWithProviders(<ClientForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(/name/i), 'Acme Farms');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));

    expect(await screen.findByText(/enter an email or a phone number/i)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('shows a submit error when one is provided', () => {
    renderWithProviders(<ClientForm onSubmit={() => {}} submitError="That client already exists." />);

    expect(screen.getByText('That client already exists.')).toBeInTheDocument();
  });
});
