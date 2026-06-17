import { List, ListItemButton, ListItemText } from '@mui/material';
import { clientsApi } from '@/features/clients/api/clientsApi';
import { QueryBoundary } from '@/shared/components/QueryBoundary';

export function ClientList({ onSelectClient }: { onSelectClient: (clientId: string) => void }) {
  const result = clientsApi.useGetClientsQuery();

  return (
    <QueryBoundary
      result={result}
      loadingLabel="Loading clients"
      errorMessage="We couldn't load clients. Please try again."
    >
      {(data) => (
        <List>
          {data.items.map((client) => (
            <ListItemButton key={client.id} onClick={() => onSelectClient(client.id)}>
              <ListItemText
                primary={client.name}
                secondary={[client.email, client.phoneNumber].filter(Boolean).join(' · ') || '—'}
              />
            </ListItemButton>
          ))}
        </List>
      )}
    </QueryBoundary>
  );
}
