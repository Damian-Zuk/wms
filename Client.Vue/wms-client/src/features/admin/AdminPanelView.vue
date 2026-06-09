<script setup lang="ts">
import Tabs from 'primevue/tabs'
import TabList from 'primevue/tablist'
import Tab from 'primevue/tab'
import TabPanels from 'primevue/tabpanels'
import TabPanel from 'primevue/tabpanel'
import PageHeader from '@/components/common/PageHeader.vue'
import AccountsTab from './AccountsTab.vue'
import DatabaseTab from './DatabaseTab.vue'
</script>

<template>
  <section class="p-6 flex flex-col gap-4" style="max-width: 1400px">
    <PageHeader title="Admin Panel" subtitle="Manage accounts and warehouse data." />

    <Tabs value="accounts">
      <TabList>
        <Tab value="accounts">Accounts</Tab>
        <Tab value="database">Database</Tab>
      </TabList>
      <TabPanels class="!px-0">
        <TabPanel value="accounts">
          <AccountsTab />
        </TabPanel>
        <TabPanel value="database">
          <DatabaseTab />
        </TabPanel>
      </TabPanels>
    </Tabs>
  </section>
</template>
