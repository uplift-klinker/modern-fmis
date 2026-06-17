import { Alert, CircularProgress, List, ListItemText } from '@mui/material';
import { clientsApi } from '@/features/clients/api/clientsApi';

export function ClientsListPage() {
  const { data, isLoading, isError } = clientsApi.useGetClientsQuery();

  if (isLoading) {
    return <CircularProgress aria-label="Loading clients" />;
  }
  if (isError) {
    return <Alert severity="error">We couldn't load clients. Please try again.</Alert>;
  }

  return (
    <List>
      {data?.items.map((client) => (
        <ListItemText
          key={client.id}
          primary={client.name}
          secondary={[client.email, client.phoneNumber].filter(Boolean).join(' · ') || '—'}
        />
      ))}
    </List>
  );
}
