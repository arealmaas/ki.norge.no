import { Alert, Flex, LinkButton } from '@strapi/design-system';
import { Bell } from '@strapi/icons';
import { NavLink } from 'react-router-dom';

interface NotificationBannerProps {
  uleste: number;
}

const NotificationBanner = ({ uleste }: NotificationBannerProps) => {
  if (uleste === 0) return null;

  return (
    <Alert
      variant="default"
      title={`Du har ${uleste} uleste varslinger`}
      action={
        <LinkButton
          tag={NavLink}
          to="/content-manager/collection-types/api::varsling.varsling"
          variant="ghost"
          size="S"
        >
          Vis alle
        </LinkButton>
      }
    >
      Klikk &laquo;Vis alle&raquo; for å se dine varslinger.
    </Alert>
  );
};

export default NotificationBanner;
