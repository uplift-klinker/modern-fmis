import { useForm } from '@tanstack/react-form';
import { Alert, Button, Stack, TextField } from '@mui/material';
import { CreateClientRequestSchema, type CreateClientRequest } from '@/features/clients/schemas/ClientSchemas';

interface ClientFormValues {
  name: string;
  email: string | null;
  phoneNumber: string | null;
}

interface ClientFormProps {
  onSubmit: (client: CreateClientRequest) => void;
  defaultValues?: ClientFormValues;
  submitError?: string;
}

const EMPTY_VALUES: ClientFormValues = { name: '', email: null, phoneNumber: null };

function extractMessages(error: unknown): string[] {
  if (!error) {
    return [];
  }
  if (typeof error === 'string') {
    return [error];
  }
  if (Array.isArray(error)) {
    return error.flatMap(extractMessages);
  }
  if (typeof error === 'object') {
    const candidate = error as { message?: unknown };
    if (typeof candidate.message === 'string') {
      return [candidate.message];
    }
    return Object.values(error as Record<string, unknown>).flatMap(extractMessages);
  }
  return [];
}

export function ClientForm({ onSubmit, defaultValues = EMPTY_VALUES, submitError }: ClientFormProps) {
  const form = useForm({
    defaultValues,
    validators: { onSubmit: CreateClientRequestSchema },
    onSubmit: ({ value }) => {
      onSubmit(value);
    },
  });

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault();
        void form.handleSubmit();
      }}
    >
      <Stack spacing={2}>
        {submitError && <Alert severity="error">{submitError}</Alert>}

        <form.Field name="name">
          {(field) => {
            const messages = extractMessages(field.state.meta.errors);
            return (
              <TextField
                label="Name"
                value={field.state.value}
                onChange={(event) => field.handleChange(event.target.value)}
                onBlur={field.handleBlur}
                error={messages.length > 0}
                helperText={messages.join(' ') || undefined}
              />
            );
          }}
        </form.Field>

        <form.Field name="email">
          {(field) => (
            <TextField
              label="Email"
              value={field.state.value ?? ''}
              onChange={(event) => field.handleChange(event.target.value)}
              onBlur={field.handleBlur}
            />
          )}
        </form.Field>

        <form.Field name="phoneNumber">
          {(field) => (
            <TextField
              label="Phone"
              value={field.state.value ?? ''}
              onChange={(event) => field.handleChange(event.target.value)}
              onBlur={field.handleBlur}
            />
          )}
        </form.Field>

        <form.Subscribe selector={(state) => state.errorMap.onSubmit}>
          {(onSubmitError) => {
            const messages = extractMessages(onSubmitError);
            return messages.length > 0 ? <Alert severity="error">{messages.join(' ')}</Alert> : null;
          }}
        </form.Subscribe>

        <Button type="submit" variant="contained">
          Save
        </Button>
      </Stack>
    </form>
  );
}
