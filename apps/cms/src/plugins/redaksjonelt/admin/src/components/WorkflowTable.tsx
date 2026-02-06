import { Box, Flex, Typography, Table, Thead, Tbody, Tr, Td, Th, Badge, LinkButton } from '@strapi/design-system';
import { ArrowRight } from '@strapi/icons';
import { NavLink } from 'react-router-dom';

interface WorkflowEntry {
  dokumentId: string;
  innholdstype: string;
  tittel: string;
  utfortAv?: string;
  tidspunkt?: string;
  publiserTid?: string;
  opprettetAv?: string;
}

interface WorkflowTableProps {
  entries: WorkflowEntry[];
  variant: 'workflow' | 'planlagt';
}

function contentTypeLabel(innholdstype: string): string {
  const map: Record<string, string> = {
    'api::artikkel.artikkel': 'artikkel',
    'api::eksempel.eksempel': 'eksempel',
    'api::veiledning.veiledning': 'veiledning',
    'api::side.side': 'side',
  };
  return map[innholdstype] || innholdstype;
}

function contentManagerUrl(innholdstype: string, dokumentId: string): string {
  const singularMap: Record<string, string> = {
    'api::artikkel.artikkel': 'api::artikkel.artikkel',
    'api::eksempel.eksempel': 'api::eksempel.eksempel',
    'api::veiledning.veiledning': 'api::veiledning.veiledning',
    'api::side.side': 'api::side.side',
  };
  const uid = singularMap[innholdstype] || innholdstype;
  return `/content-manager/collection-types/${uid}/${dokumentId}`;
}

function formatTime(iso?: string): string {
  if (!iso) return '';
  const date = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);

  if (diffMin < 1) return 'akkurat nå';
  if (diffMin < 60) return `${diffMin} min siden`;
  const diffHours = Math.floor(diffMin / 60);
  if (diffHours < 24) return `${diffHours} t siden`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 7) return `${diffDays} d siden`;
  return date.toLocaleDateString('nb-NO');
}

function formatFutureTime(iso?: string): string {
  if (!iso) return '';
  return new Date(iso).toLocaleString('nb-NO', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  });
}

const WorkflowTable = ({ entries, variant }: WorkflowTableProps) => {
  if (entries.length === 0) {
    return (
      <Box padding={4}>
        <Typography variant="omega" textColor="neutral600">
          Ingen oppføringer
        </Typography>
      </Box>
    );
  }

  return (
    <Table>
      <Thead>
        <Tr>
          <Th>
            <Typography variant="sigma">Tittel</Typography>
          </Th>
          <Th>
            <Typography variant="sigma">Type</Typography>
          </Th>
          <Th>
            <Typography variant="sigma">
              {variant === 'planlagt' ? 'Opprettet av' : 'Sendt av'}
            </Typography>
          </Th>
          <Th>
            <Typography variant="sigma">
              {variant === 'planlagt' ? 'Publiseres' : 'Tidspunkt'}
            </Typography>
          </Th>
          <Th>
            <Typography variant="sigma">Handling</Typography>
          </Th>
        </Tr>
      </Thead>
      <Tbody>
        {entries.map((entry) => (
          <Tr key={`${entry.innholdstype}-${entry.dokumentId}`}>
            <Td>
              <Typography variant="omega" fontWeight="bold">
                {entry.tittel}
              </Typography>
            </Td>
            <Td>
              <Badge variant="secondary">{contentTypeLabel(entry.innholdstype)}</Badge>
            </Td>
            <Td>
              <Typography variant="omega" textColor="neutral600">
                {variant === 'planlagt' ? entry.opprettetAv : entry.utfortAv}
              </Typography>
            </Td>
            <Td>
              <Typography variant="omega" textColor="neutral600">
                {variant === 'planlagt'
                  ? formatFutureTime(entry.publiserTid)
                  : formatTime(entry.tidspunkt)}
              </Typography>
            </Td>
            <Td>
              <LinkButton
                tag={NavLink}
                to={contentManagerUrl(entry.innholdstype, entry.dokumentId)}
                variant="ghost"
                endIcon={<ArrowRight />}
                size="S"
              >
                Åpne
              </LinkButton>
            </Td>
          </Tr>
        ))}
      </Tbody>
    </Table>
  );
};

export default WorkflowTable;
