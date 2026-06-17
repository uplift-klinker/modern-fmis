import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle } from '@mui/material';
import { clientsApi } from '@/features/clients/api/clientsApi';
import { ClientForm } from '@/features/clients/components/ClientForm';
import type { ClientResponse, CreateClientRequest } from '@/features/clients/schemas/ClientSchemas';

interface CreateClientDialogProps {
  open: boolean;
  onClose: () => void;
  onCreated: (client: ClientResponse) => void;
}

export function CreateClientDialog({ open, onClose, onCreated }: CreateClientDialogProps) {
  const [createClient] = clientsApi.useCreateClientMutation();
  const [submitError, setSubmitError] = useState<string | undefined>(undefined);

  async function handleSubmit(values: CreateClientRequest) {
    try {
      const created = await createClient(values).unwrap();
      onCreated(created);
    } catch {
      setSubmitError("We couldn't create the client. Please check the details and try again.");
    }
  }

  return (
    <Dialog open={open} onClose={onClose}>
      <DialogTitle>New Client</DialogTitle>
      <DialogContent>
        <ClientForm onSubmit={handleSubmit} submitError={submitError} />
      </DialogContent>
    </Dialog>
  );
}
