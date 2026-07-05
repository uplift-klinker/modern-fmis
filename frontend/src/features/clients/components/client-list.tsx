import { ListItemButton, ListItemText } from "@mui/material";
import { clientsApi } from "@/features/clients/api/clients-api";
import { QueryBoundary } from "@/shared/components/query-boundary";
import { EntityList } from "@/shared/components/entity-list";

export function ClientList({ onSelectClient }: { onSelectClient: (clientId: string) => void }) {
  const result = clientsApi.useGetClientsQuery();

  return (
    <QueryBoundary
      result={result}
      loadingLabel="Loading clients"
      errorMessage="We couldn't load clients. Please try again."
    >
      {(data) => (
        <EntityList
          items={data.items}
          emptyMessage="No clients yet."
          onRefresh={result.refetch}
          getKey={(client) => client.id}
          renderItem={(client) => (
            <ListItemButton onClick={() => onSelectClient(client.id)}>
              <ListItemText
                primary={client.name}
                secondary={[client.email, client.phoneNumber].filter(Boolean).join(" · ") || "—"}
              />
            </ListItemButton>
          )}
        />
      )}
    </QueryBoundary>
  );
}
