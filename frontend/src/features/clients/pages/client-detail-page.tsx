import { useParams } from "react-router-dom";
import { ClientDetail } from "@/features/clients/components/client-detail";

export function ClientDetailPage() {
  const { id = "" } = useParams();
  return <ClientDetail clientId={id} />;
}
