import { Box, Stack, Typography } from '@mui/material';
import { clientsApi } from '@/features/clients/api/clientsApi';
import { QueryBoundary } from '@/shared/components/QueryBoundary';

export function ClientDetail({ clientId }: { clientId: string }) {
  const result = clientsApi.useGetClientQuery(clientId);

  return (
    <QueryBoundary result={result} loadingLabel="Loading client" errorMessage="Client not found.">
      {(client) => (
        <Box>
          <Typography variant="h5" component="h1">
            {client.name}
          </Typography>
          <Stack spacing={1} sx={{ mt: 2 }}>
            <Typography>Email: {client.email ?? '—'}</Typography>
            <Typography>Phone: {client.phoneNumber ?? '—'}</Typography>
          </Stack>
        </Box>
      )}
    </QueryBoundary>
  );
}
