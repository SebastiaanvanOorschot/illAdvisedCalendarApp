import { AxiosRequestConfig as Config } from 'axios';

export default class BaseAPI {
  transformOptions(options: Config): Promise<Config> {
    return new Promise((resolve) => {
      // Add any default headers or configuration here
      options.headers = {
        ...options.headers,
        'Content-Type': 'application/json',
      };

      // Inject JWT token from localStorage
      const accessToken = localStorage.getItem('accessToken');
      if (accessToken) {
        options.headers = {
          ...options.headers,
          'Authorization': `Bearer ${accessToken}`,
        };
      }

      resolve(options);
    });
  }
}
