import { useParams } from 'react-router-dom';
import { ClientDetail } from '@/features/clients/components/ClientDetail';

export function ClientDetailPage() {
  const { id = '' } = useParams();
  return <ClientDetail clientId={id} />;
}
