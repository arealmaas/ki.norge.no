import { Box, Flex, Grid, Typography } from '@strapi/design-system';

interface StatCardsProps {
  antall: {
    til_godkjenning: number;
    godkjent: number;
    avvist: number;
    planlagt: number;
  };
}

const cards = [
  { key: 'til_godkjenning' as const, label: 'Til godkjenning', color: 'warning600' },
  { key: 'godkjent' as const, label: 'Godkjent', color: 'success600' },
  { key: 'avvist' as const, label: 'Avvist', color: 'danger600' },
  { key: 'planlagt' as const, label: 'Planlagt', color: 'primary600' },
];

const StatCards = ({ antall }: StatCardsProps) => {
  return (
    <Grid.Root gridCols={4} gap={4}>
      {cards.map(({ key, label, color }) => (
        <Grid.Item key={key} col={1} direction="column">
          <Box
            background="neutral0"
            shadow="tableShadow"
            padding={5}
            hasRadius
            width="100%"
          >
            <Flex direction="column" alignItems="center" gap={2}>
              <Typography variant="pi" fontWeight="bold" textColor={color}>
                {label}
              </Typography>
              <Typography variant="alpha" textColor={color}>
                {antall[key]}
              </Typography>
            </Flex>
          </Box>
        </Grid.Item>
      ))}
    </Grid.Root>
  );
};

export default StatCards;
