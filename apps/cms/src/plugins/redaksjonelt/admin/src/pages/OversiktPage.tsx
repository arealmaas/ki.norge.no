import { useEffect, useState, useCallback } from 'react';
import { Box, Flex, Typography, Tabs, Loader } from '@strapi/design-system';
import { Page, Layouts, useFetchClient } from '@strapi/strapi/admin';
import StatCards from '../components/StatCards';
import WorkflowTable from '../components/WorkflowTable';
import NotificationBanner from '../components/NotificationBanner';

interface WorkflowEntry {
  dokumentId: string;
  innholdstype: string;
  tittel: string;
  utfortAv?: string;
  tidspunkt?: string;
  publiserTid?: string;
  opprettetAv?: string;
}

interface OversiktData {
  oversikt: {
    til_godkjenning: WorkflowEntry[];
    godkjent: WorkflowEntry[];
    avvist: WorkflowEntry[];
    planlagt: WorkflowEntry[];
  };
  antall: {
    til_godkjenning: number;
    godkjent: number;
    avvist: number;
    planlagt: number;
  };
  varslinger: {
    uleste: number;
  };
}

const REFETCH_INTERVAL = 30_000;

const OversiktPage = () => {
  const { get } = useFetchClient();
  const [data, setData] = useState<OversiktData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = useCallback(async () => {
    try {
      const response = await get<{ data: OversiktData }>('/api/redaksjonelt/oversikt');
      setData(response.data.data);
      setError(null);
    } catch (err) {
      setError('Kunne ikke laste oversikt');
    } finally {
      setLoading(false);
    }
  }, [get]);

  useEffect(() => {
    fetchData();
    const interval = setInterval(fetchData, REFETCH_INTERVAL);
    return () => clearInterval(interval);
  }, [fetchData]);

  if (loading) {
    return (
      <Page.Main>
        <Page.Title>Redaksjonelt</Page.Title>
        <Layouts.Header title="Redaksjonell oversikt" />
        <Layouts.Content>
          <Flex justifyContent="center" padding={10}>
            <Loader>Laster oversikt…</Loader>
          </Flex>
        </Layouts.Content>
      </Page.Main>
    );
  }

  if (error || !data) {
    return (
      <Page.Main>
        <Page.Title>Redaksjonelt</Page.Title>
        <Layouts.Header title="Redaksjonell oversikt" />
        <Layouts.Content>
          <Box padding={8}>
            <Typography variant="omega" textColor="danger600">
              {error || 'Kunne ikke laste oversikt'}
            </Typography>
          </Box>
        </Layouts.Content>
      </Page.Main>
    );
  }

  const { oversikt, antall, varslinger } = data;

  return (
    <Page.Main>
      <Page.Title>Redaksjonelt</Page.Title>
      <Layouts.Header
        title="Redaksjonell oversikt"
        subtitle="Arbeidsflyt, publisering og varslinger"
      />
      <Layouts.Content>
        <Flex direction="column" gap={6}>
          {/* Notification banner */}
          <NotificationBanner uleste={varslinger.uleste} />

          {/* Summary cards */}
          <StatCards antall={antall} />

          {/* Tabbed article lists */}
          <Box background="neutral0" shadow="tableShadow" hasRadius>
            <Tabs.Root defaultValue="til_godkjenning">
              <Tabs.List>
                <Tabs.Trigger value="til_godkjenning">
                  Til godkjenning ({antall.til_godkjenning})
                </Tabs.Trigger>
                <Tabs.Trigger value="godkjent">
                  Godkjent ({antall.godkjent})
                </Tabs.Trigger>
                <Tabs.Trigger value="avvist">
                  Avvist ({antall.avvist})
                </Tabs.Trigger>
                <Tabs.Trigger value="planlagt">
                  Planlagt ({antall.planlagt})
                </Tabs.Trigger>
              </Tabs.List>
              <Tabs.Content value="til_godkjenning">
                <WorkflowTable entries={oversikt.til_godkjenning} variant="workflow" />
              </Tabs.Content>
              <Tabs.Content value="godkjent">
                <WorkflowTable entries={oversikt.godkjent} variant="workflow" />
              </Tabs.Content>
              <Tabs.Content value="avvist">
                <WorkflowTable entries={oversikt.avvist} variant="workflow" />
              </Tabs.Content>
              <Tabs.Content value="planlagt">
                <WorkflowTable entries={oversikt.planlagt} variant="planlagt" />
              </Tabs.Content>
            </Tabs.Root>
          </Box>
        </Flex>
      </Layouts.Content>
    </Page.Main>
  );
};

export default OversiktPage;
