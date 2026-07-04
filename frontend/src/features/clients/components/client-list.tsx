import { List, ListItemButton, ListItemText } from "@mui/material";
import { clientsApi } from "@/features/clients/api/clients-api";
import { QueryBoundary } from "@/shared/components/query-boundary";
import { EmptyState } from "@/shared/components/empty-state";

export function ClientList({ onSelectClient }: { onSelectClient: (clientId: string) => void }) {
  const result = clientsApi.useGetClientsQuery();

  return (
    <QueryBoundary
      result={result}
      loadingLabel="Loading clients"
      errorMessage="We couldn't load clients. Please try again."
    >
      {(data) =>
        data.items.length === 0 ? (
          <EmptyState message="No clients yet." onRefresh={result.refetch} />
        ) : (
          <List>
            {data.items.map((client) => (
              <ListItemButton key={client.id} onClick={() => onSelectClient(client.id)}>
                <ListItemText
                  primary={client.name}
                  secondary={[client.email, client.phoneNumber].filter(Boolean).join(" · ") || "—"}
                />
              </ListItemButton>
            ))}
          </List>
        )
      }
    </QueryBoundary>
  );
}
