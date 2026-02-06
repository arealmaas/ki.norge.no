import { Routes, Route } from 'react-router-dom';
import { Page } from '@strapi/strapi/admin';
import OversiktPage from './OversiktPage';

const App = () => {
  return (
    <Routes>
      <Route index element={<OversiktPage />} />
      <Route path="*" element={<Page.Error />} />
    </Routes>
  );
};

export default App;
